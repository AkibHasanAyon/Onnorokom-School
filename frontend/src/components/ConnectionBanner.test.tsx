import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import { connectionProblemKind } from '../hooks/useConnectionProblem';
import { givenClasses } from '../test/api';
import { morningShift as morning, STUDENT } from '../test/fixtures';
import { problem } from '../test/handlers';
import { renderApp } from '../test/render';
import { server } from '../test/server';

const banner = () => screen.queryByText(/Retrying…/);

describe('connection banner and stale data (R12)', () => {
  it("says it can't reach the server while refreshes fail, keeps the last data, and clears itself", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    givenClasses(morning);
    renderApp('/');
    await screen.findByRole('cell', { name: '25' });
    expect(banner()).not.toBeInTheDocument();
    expect(screen.getByText(/^Updated (just now|\d+ s ago)$/)).toBeInTheDocument();

    server.use(http.get('*/classes', () => HttpResponse.error()));
    await vi.advanceTimersByTimeAsync(3_000);

    const status = await screen.findByText(/Can't reach the server\. Showing data from \d+ s ago\. Retrying…/);
    expect(status.closest('[role="status"]')).not.toBeNull();
    expect(screen.getByRole('cell', { name: '25' })).toBeInTheDocument(); // last numbers still shown
    expect(within(screen.getByRole('main')).getByText(/Couldn't refresh — showing data from \d+ s ago, it may be out of date\./)).toBeInTheDocument();

    server.use(http.get('*/classes', () => HttpResponse.json([{ ...morning, availableSeats: 24 }])));
    await vi.advanceTimersByTimeAsync(3_000);

    expect(await screen.findByRole('cell', { name: '24' })).toBeInTheDocument();
    expect(banner()).not.toBeInTheDocument();
    expect(screen.queryByText(/Couldn't refresh/)).not.toBeInTheDocument();
  });

  it('shows the banner without an age when nothing has loaded yet', async () => {
    server.use(http.get('*/classes', () => HttpResponse.error()));

    renderApp('/');

    expect(await screen.findByText(/^Can't reach the server\. Retrying…$/)).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent("Can't reach the server. Check your internet connection — we'll keep retrying.");
  });

  it('says the server is having trouble on 5xx answers', async () => {
    server.use(http.get('*/classes', () => problem(503, 'SERVICE_UNAVAILABLE')));

    renderApp('/');

    expect(await screen.findByText(/^The server is having trouble right now\. Retrying…$/)).toBeInTheDocument();
  });

  it('does not show the banner for an ordinary "not found"', async () => {
    renderApp('/applications/0193a6f3-0a11-7c22-8e33-445566778899'); // default: 404 HOLD_NOT_FOUND

    expect(await screen.findByRole('alert')).toHaveTextContent("We couldn't find this application.");
    expect(banner()).not.toBeInTheDocument();
  });

  it('goes away when you leave the page whose refresh was failing', async () => {
    server.use(http.get('*/classes', () => HttpResponse.error()));
    renderApp('/');
    expect(await screen.findByText(/Retrying…/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('link', { name: 'Admin: Create Class' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Create a class' })).toBeInTheDocument();
    expect(banner()).not.toBeInTheDocument();
  });

  it('marks a failing My Applications refresh as possibly out of date', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    server.use(
      http.get('*/me/holds', () =>
        HttpResponse.json([
          { holdId: 'h1', classId: 'c1', className: 'Class 6 - Morning Shift', status: 'CONFIRMED', expiresAt: new Date().toISOString() },
        ]),
      ),
    );
    renderApp('/my-applications', { studentId: STUDENT });
    await screen.findByText('Class 6 - Morning Shift');

    server.use(http.get('*/me/holds', () => HttpResponse.error()));
    await vi.advanceTimersByTimeAsync(5_000);

    expect(await screen.findByText(/Couldn't refresh/)).toBeInTheDocument();
    expect(screen.getByText('Class 6 - Morning Shift')).toBeInTheDocument();
    expect(banner()).toBeInTheDocument();
  });
});

describe('connectionProblemKind', () => {
  it.each([
    [new ApiError('network', null, 'NETWORK_ERROR', ''), 'network'],
    [new ApiError('timeout', null, 'TIMEOUT', ''), 'network'],
    [new ApiError('http', 503, 'SERVICE_UNAVAILABLE', ''), 'server'],
    [new ApiError('http', 500, 'HTTP_500', ''), 'server'],
    [new ApiError('http', 404, 'HOLD_NOT_FOUND', ''), null],
    [new ApiError('http', 409, 'NO_SEATS_AVAILABLE', ''), null],
    [new Error('boom'), null],
  ])('%s → %s', (error, kind) => {
    expect(connectionProblemKind(error)).toBe(kind);
  });
});
