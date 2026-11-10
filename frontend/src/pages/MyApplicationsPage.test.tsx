import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { HoldStatus, MyHold } from '../api/types';
import { givenMyHoldsByStudent as givenHolds } from '../test/api';
import { aMyHold, inSeconds, OTHER_STUDENT as OTHER, STUDENT } from '../test/fixtures';
import { problem } from '../test/handlers';
import { renderApp } from '../test/render';
import { server } from '../test/server';

function mine(holdId: string, className: string, status: HoldStatus, expiresIn = 462, createdAgo = 60): MyHold {
  return aMyHold({ holdId, classId: `c-${holdId}`, className, status, expiresAt: inSeconds(expiresIn), createdAt: inSeconds(-createdAgo) });
}

const held = mine('h1', 'Class 6 - Morning Shift', 'HELD', 462, 30);
const confirmed = mine('h2', 'Class 7 - Morning Shift', 'CONFIRMED', -100, 10);
const expired = mine('h3', 'Class 5 - Day Shift', 'EXPIRED', -600, 900);

const bodyRows = () => within(screen.getByRole('table')).getAllByRole('row').slice(1);
const cellsOf = (row: HTMLElement) => [
  within(row).getByRole('rowheader').textContent,
  ...within(row).getAllByRole('cell').slice(0, 2).map((c) => c.textContent),
];

describe('MyApplicationsPage', () => {
  it('lists every application for the saved number: active first, then newest (R11)', async () => {
    const asked = givenHolds({ [STUDENT]: [confirmed, held, expired] });

    renderApp('/my-applications', { studentId: STUDENT });

    expect(await screen.findByRole('heading', { level: 1, name: `My applications (${STUDENT})` })).toBeInTheDocument();
    await screen.findByRole('table');
    const rows = bodyRows();
    expect(rows).toHaveLength(3);
    expect(cellsOf(rows[0]!)[0]).toBe('Class 6 - Morning Shift');
    expect(cellsOf(rows[0]!)[1]).toBe('HELD');
    expect(cellsOf(rows[0]!)[2]).toMatch(/^07:4[12]$/);
    expect(cellsOf(rows[1]!)).toEqual(['Class 7 - Morning Shift', 'CONFIRMED', '—']);
    expect(cellsOf(rows[2]!)).toEqual(['Class 5 - Day Shift', 'EXPIRED', '—']);
    expect(asked[0]).toBe(STUDENT);
  });

  it('links each row to its application page', async () => {
    givenHolds({ [STUDENT]: [held] });

    renderApp('/my-applications', { studentId: STUDENT });

    const link = await screen.findByRole('link', { name: 'Open application for Class 6 - Morning Shift' });
    expect(link).toHaveAttribute('href', '/applications/h1');
    expect(link).toHaveTextContent('Open');
  });

  it('opens the application when "Open" is clicked', async () => {
    givenHolds({ [STUDENT]: [held] });
    server.use(
      http.get('*/holds/h1', () =>
        HttpResponse.json({ holdId: 'h1', classId: held.classId, className: held.className, status: 'HELD', expiresAt: held.expiresAt }),
      ),
    );
    renderApp('/my-applications', { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('link', { name: 'Open application for Class 6 - Morning Shift' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Application — Class 6 - Morning Shift' })).toBeInTheDocument();
  });

  it('switches an active row to EXPIRED when its countdown reaches 0, without a reload', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    givenHolds({ [STUDENT]: [mine('h9', 'Class 8', 'HELD', 2)] });
    renderApp('/my-applications', { studentId: STUDENT });
    await screen.findByRole('table');
    expect(cellsOf(bodyRows()[0]!)[1]).toBe('HELD');

    await vi.advanceTimersByTimeAsync(2_500);

    expect(cellsOf(bodyRows()[0]!)).toEqual(['Class 8', 'EXPIRED', '—']);
  });

  it('refreshes every 5 seconds', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const list = [held];
    givenHolds({ [STUDENT]: list });
    renderApp('/my-applications', { studentId: STUDENT });
    await screen.findByRole('table');

    list[0] = { ...held, status: 'CONFIRMED' };
    await vi.advanceTimersByTimeAsync(5_000);

    expect(await within(screen.getByRole('table')).findByText('CONFIRMED')).toBeInTheDocument();
  });

  it('asks for a number when none is saved, without calling the server', async () => {
    const asked = givenHolds({});

    renderApp('/my-applications');

    expect(screen.getByRole('heading', { level: 1, name: 'My applications' })).toBeInTheDocument();
    expect(screen.getByText(/Enter the student's birth registration number at the top of the page to see applications/)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(asked).toEqual([]);
  });

  it('says so when there are no applications yet, with a link to the classes', async () => {
    givenHolds({});

    renderApp('/my-applications', { studentId: STUDENT });

    expect(await screen.findByText(/No applications yet/)).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByRole('link', { name: 'Classes' })).toHaveAttribute('href', '/');
  });

  it('shows the new number\'s applications after the number is changed', async () => {
    givenHolds({ [STUDENT]: [held], [OTHER]: [confirmed] });
    renderApp('/my-applications', { studentId: STUDENT });
    await screen.findByText('Class 6 - Morning Shift');

    await userEvent.click(screen.getByRole('button', { name: 'Change' }));
    await userEvent.type(screen.getByLabelText('Birth Registration No.'), OTHER);

    expect(await screen.findByText('Class 7 - Morning Shift')).toBeInTheDocument();
    expect(screen.queryByText('Class 6 - Morning Shift')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: `My applications (${OTHER})` })).toBeInTheDocument();
  });

  it('shows a friendly message and Retry when loading fails', async () => {
    server.use(http.get('*/me/holds', () => problem(503, 'SERVICE_UNAVAILABLE')));
    renderApp('/my-applications', { studentId: STUDENT });

    expect(await screen.findByRole('alert')).toHaveTextContent('The server is having trouble right now. Please try again in a moment.');

    givenHolds({ [STUDENT]: [held] });
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('Class 6 - Morning Shift')).toBeInTheDocument();
  });
});
