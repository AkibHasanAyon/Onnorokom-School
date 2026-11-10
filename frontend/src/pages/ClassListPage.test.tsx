import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { HoldCreated, HoldDetails } from '../api/types';
import { givenClasses, givenMyHolds } from '../test/api';
import { dayShift as day, inMinutes, morningShift as morning, STUDENT } from '../test/fixtures';
import { problem } from '../test/handlers';
import { renderApp } from '../test/render';
import { server } from '../test/server';

const row = (name: string) => screen.getByRole('row', { name: new RegExp(name) });

describe('ClassListPage', () => {
  it('shows every class with total, held, confirmed and available seats', async () => {
    givenClasses(morning, day);

    renderApp('/');

    const r = await screen.findByRole('row', { name: /Morning Shift/ });
    expect(within(r).getAllByRole('cell').slice(0, 4).map((c) => c.textContent)).toEqual(['120', '15', '80', '25']);
    expect(screen.getByRole('columnheader', { name: 'Available' })).toBeInTheDocument();
  });

  it('refreshes seat numbers every 3 seconds without a reload (R10)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let available = 25;
    server.use(http.get('*/classes', () => HttpResponse.json([{ ...morning, availableSeats: available }])));

    renderApp('/');
    await screen.findByRole('cell', { name: '25' });

    available = 24;
    await vi.advanceTimersByTimeAsync(3_000);

    expect(await screen.findByRole('cell', { name: '24' })).toBeInTheDocument();
    expect(screen.queryByRole('cell', { name: '25' })).not.toBeInTheDocument();
  });

  it('keeps showing the last numbers when a refresh fails', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    givenClasses(morning);
    renderApp('/');
    await screen.findByRole('cell', { name: '25' });

    server.use(http.get('*/classes', () => HttpResponse.error()));
    await vi.advanceTimersByTimeAsync(3_000);

    expect(screen.getByRole('cell', { name: '25' })).toBeInTheDocument();
  });

  it('disables Apply and explains why when no birth registration number is entered', async () => {
    givenClasses(morning);

    renderApp('/');

    const button = await within(await screen.findByRole('row', { name: /Morning Shift/ })).findByRole('button', { name: 'Apply' });
    expect(button).toBeDisabled();
    expect(button).toHaveAccessibleDescription('Enter a birth registration number at the top of the page to apply.');
  });

  it('shows "Full" (disabled) when no seat is available', async () => {
    givenClasses(day);

    renderApp('/', { studentId: STUDENT });

    expect(await within(await screen.findByRole('row', { name: /Day Shift/ })).findByRole('button', { name: 'Full' })).toBeDisabled();
  });

  it('applies and opens the new application', async () => {
    givenClasses(morning);
    let sentStudent: string | null = null;
    const created: HoldCreated = {
      holdId: 'h-new', classId: morning.id, studentId: STUDENT, status: 'HELD', expiresAt: inMinutes(10),
    };
    server.use(
      http.post('*/classes/:classId/holds', ({ request, params }) => {
        sentStudent = request.headers.get('X-Student-Id');
        expect(params.classId).toBe(morning.id);
        return HttpResponse.json(created, { status: 201 });
      }),
      http.get('*/holds/h-new', () => HttpResponse.json({ ...created } satisfies HoldDetails)),
    );
    renderApp('/', { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Apply for Class 6 - Morning Shift' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Application' })).toBeInTheDocument();
    expect(sentStudent).toBe(STUDENT);
  });

  it('shows "Applying…" and blocks double clicks while the request is in flight', async () => {
    givenClasses(morning, { ...day, id: 'c-other', name: 'Class 7', availableSeats: 3 });
    server.use(http.post('*/classes/:classId/holds', async () => { await delay('infinite'); return HttpResponse.json({}); }));
    renderApp('/', { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Apply for Class 6 - Morning Shift' }));

    expect(await screen.findByRole('button', { name: 'Applying…' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Apply for Class 7' })).toBeDisabled();
  });

  it('explains a full class in plain language (409 NO_SEATS_AVAILABLE)', async () => {
    givenClasses(morning);
    server.use(http.post('*/classes/:classId/holds', () => problem(409, 'NO_SEATS_AVAILABLE')));
    renderApp('/', { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Apply for Class 6 - Morning Shift' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/all seats in this class are taken right now/i);
  });

  it('links to the existing application on 409 ACTIVE_HOLD_EXISTS', async () => {
    givenClasses(morning);
    server.use(http.post('*/classes/:classId/holds', () => problem(409, 'ACTIVE_HOLD_EXISTS', { existingHoldId: 'h-old' })));
    renderApp('/', { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Apply for Class 6 - Morning Shift' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('You already have an active application for this class.');
    expect(within(alert).getByRole('link', { name: 'Open it' })).toHaveAttribute('href', '/applications/h-old');
  });

  it('offers "View application" for an active hold and "Admitted ✓" for a confirmed one', async () => {
    givenClasses(morning, { ...day, availableSeats: 5 });
    givenMyHolds(
      { holdId: 'h1', classId: morning.id, className: morning.name, status: 'HELD', expiresAt: inMinutes(5) },
      { holdId: 'h2', classId: day.id, className: day.name, status: 'CONFIRMED', expiresAt: inMinutes(-30) },
    );

    renderApp('/', { studentId: STUDENT });

    expect(await within(await screen.findByRole('row', { name: /Morning Shift/ })).findByRole('link', { name: 'View application' }))
      .toHaveAttribute('href', '/applications/h1');
    expect(await within(row('Day Shift')).findByRole('link', { name: 'Admitted ✓' })).toHaveAttribute('href', '/applications/h2');
  });

  it('offers Apply again when the student\'s hold has run out', async () => {
    givenClasses(morning);
    givenMyHolds({ holdId: 'h1', classId: morning.id, className: morning.name, status: 'HELD', expiresAt: inMinutes(-1) });

    renderApp('/', { studentId: STUDENT });

    expect(await screen.findByRole('button', { name: 'Apply for Class 6 - Morning Shift' })).toBeEnabled();
  });

  it('shows a helpful empty state', async () => {
    renderApp('/');

    expect(await screen.findByText(/No classes yet/)).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByRole('link', { name: 'Admin: Create Class' }))
      .toHaveAttribute('href', '/admin/classes/new');
  });

  it('shows a friendly error with Retry when classes cannot be loaded, and recovers', async () => {
    server.use(http.get('*/classes', () => HttpResponse.error()));
    renderApp('/');

    expect(await screen.findByRole('alert')).toHaveTextContent(/Can't reach the server/);

    givenClasses(morning);
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
    expect(screen.getByRole('cell', { name: '25' })).toBeInTheDocument();
  });
});
