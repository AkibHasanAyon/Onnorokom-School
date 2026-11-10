import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { ClassSummary, HoldDetails, HoldStatus } from '../api/types';
import { ErrorBoundary } from '../components/ErrorBoundary';
import { messages } from '../i18n/messages';
import { givenClasses, givenHold, givenMyHolds } from '../test/api';
import { aHold, aMyHold, HOLD_ID, inSeconds, morningShift, STUDENT } from '../test/fixtures';
import { problem } from '../test/handlers';
import { renderApp } from '../test/render';
import { server } from '../test/server';

/** Testing_QA.md §5: expiry countdown, confirm flow and error boundary — across screens. */

const main = () => within(screen.getByRole('main'));
const badgeText = () => main().getByText(/^(HELD|CONFIRMED|EXPIRED|CANCELLED)$/).textContent;
const PATH = `/applications/${HOLD_ID}`;

/**
 * A tiny stateful fake of the backend for one class and one student, enforcing the same rules as the
 * real API: seats = held (not expired) + confirmed, pay/cancel only while HELD.
 */
function fakeBackend(cls: ClassSummary, holdSeconds = 600) {
  const holds: HoldDetails[] = [];
  const effective = (h: HoldDetails): HoldStatus =>
    h.status === 'HELD' && Date.parse(h.expiresAt) <= Date.now() ? 'EXPIRED' : h.status;
  const view = (h: HoldDetails): HoldDetails => ({ ...h, status: effective(h) });
  const summary = (): ClassSummary => {
    const held = holds.filter((h) => effective(h) === 'HELD').length;
    const confirmed = holds.filter((h) => h.status === 'CONFIRMED').length;
    return { ...cls, heldSeats: held, confirmedSeats: confirmed, availableSeats: cls.totalSeats - held - confirmed };
  };
  const find = (id: unknown) => holds.find((h) => h.holdId === id);

  server.use(
    http.get('*/classes', () => HttpResponse.json([summary()])),
    http.post('*/classes/:classId/holds', () => {
      if (summary().availableSeats === 0) return problem(409, 'NO_SEATS_AVAILABLE');
      const hold = aHold({ holdId: `h-${holds.length + 1}`, classId: cls.id, className: cls.name, expiresAt: inSeconds(holdSeconds) });
      holds.push(hold);
      return HttpResponse.json({ ...hold, studentId: STUDENT }, { status: 201 });
    }),
    http.get('*/holds/:holdId', ({ params }) => {
      const hold = find(params.holdId);
      return hold ? HttpResponse.json(view(hold)) : problem(404, 'HOLD_NOT_FOUND');
    }),
    http.post('*/holds/:holdId/confirm', ({ params }) => {
      const hold = find(params.holdId);
      if (!hold) return problem(404, 'HOLD_NOT_FOUND');
      if (effective(hold) === 'EXPIRED') return problem(409, 'HOLD_EXPIRED');
      hold.status = 'CONFIRMED';
      return HttpResponse.json({ holdId: hold.holdId, status: 'CONFIRMED' });
    }),
    http.delete('*/holds/:holdId', ({ params }) => {
      const hold = find(params.holdId);
      if (!hold) return problem(404, 'HOLD_NOT_FOUND');
      if (hold.status === 'CONFIRMED') return problem(409, 'HOLD_ALREADY_CONFIRMED');
      if (effective(hold) === 'EXPIRED') return problem(409, 'HOLD_EXPIRED');
      hold.status = 'CANCELLED';
      return new HttpResponse(null, { status: 204 });
    }),
    http.get('*/me/holds', () => HttpResponse.json([...holds].reverse().map((h) => aMyHold(view(h))))),
  );
  return { holds, summary };
}

const applyFor = (name: string) => screen.findByRole('button', { name: `Apply for ${name}` });
const goTo = (link: string) => userEvent.click(screen.getByRole('link', { name: link }));

describe('apply → pay → admitted', () => {
  it('walks the whole journey and every screen agrees on the result', async () => {
    const backend = fakeBackend({ ...morningShift, totalSeats: 2 });
    renderApp('/', { studentId: STUDENT });

    await userEvent.click(await applyFor(morningShift.name));

    expect(await screen.findByRole('heading', { level: 1, name: `Application — ${morningShift.name}` })).toBeInTheDocument();
    expect(badgeText()).toBe('HELD');
    expect(screen.getByTestId('countdown')).toHaveTextContent(/^(10:00|09:5\d)$/);

    await userEvent.click(screen.getByRole('button', { name: 'Pay Admission Fee' }));
    expect(await main().findByText('CONFIRMED')).toBeInTheDocument();
    expect(backend.summary()).toMatchObject({ confirmedSeats: 1, heldSeats: 0, availableSeats: 1 });

    await goTo('Classes');
    const row = await screen.findByRole('row', { name: /Morning Shift/ });
    expect(await within(row).findByRole('link', { name: 'Admitted ✓' })).toHaveAttribute('href', '/applications/h-1');
    expect(within(row).getAllByRole('cell').slice(0, 4).map((c) => c.textContent)).toEqual(['2', '0', '1', '1']);

    await goTo('My Applications');
    const myRow = await screen.findByRole('row', { name: /Morning Shift/ });
    expect(within(myRow).getByText('CONFIRMED')).toBeInTheDocument();
  });

  it('cancelling returns the seat at once and lets the student apply again', async () => {
    const backend = fakeBackend({ ...morningShift, totalSeats: 1 });
    renderApp('/', { studentId: STUDENT });
    await userEvent.click(await applyFor(morningShift.name));
    await screen.findByRole('button', { name: 'Cancel application' });

    await userEvent.click(screen.getByRole('button', { name: 'Cancel application' }));
    await userEvent.click(screen.getByRole('button', { name: 'Yes, cancel it' }));
    expect(await main().findByText('CANCELLED')).toBeInTheDocument();
    expect(backend.summary().availableSeats).toBe(1);

    await goTo('Back to classes');
    expect(await applyFor(morningShift.name)).toBeEnabled();
  });
});

describe('expiry countdown (R9)', () => {
  it('expires on the application page, then the class list offers Apply again — all without a reload', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const backend = fakeBackend({ ...morningShift, totalSeats: 1 }, 5);
    renderApp('/', { studentId: STUDENT });
    await userEvent.click(await applyFor(morningShift.name));
    await screen.findByTestId('countdown');

    await vi.advanceTimersByTimeAsync(5_500);

    expect(badgeText()).toBe('EXPIRED');
    expect(screen.getByRole('button', { name: 'Pay Admission Fee' })).toBeDisabled();
    expect(backend.summary().availableSeats).toBe(1);

    await goTo('Back to classes');
    expect(await applyFor(morningShift.name)).toBeEnabled();
  });

  it('turns "View application" back into Apply on the class list when the hold runs out', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    givenClasses(morningShift);
    givenMyHolds(aMyHold({ expiresAt: inSeconds(2) }));
    renderApp('/', { studentId: STUDENT });
    const row = await screen.findByRole('row', { name: /Morning Shift/ });
    expect(await within(row).findByRole('link', { name: 'View application' })).toBeInTheDocument();

    await vi.advanceTimersByTimeAsync(2_500);

    expect(await applyFor(morningShift.name)).toBeEnabled();
  });

  it('shows hours for long holds (h:mm:ss)', async () => {
    const long = aHold({ expiresAt: inSeconds(2 * 3600) });
    givenHold(() => long);

    renderApp(PATH, { studentId: STUDENT });

    expect(await screen.findByTestId('countdown')).toHaveTextContent(/^(2:00:00|1:59:5\d)$/);
  });

  it('closes the cancel dialog if the time runs out while it is open', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const expiring = aHold({ expiresAt: inSeconds(3) }); // built once: the deadline must not move
    givenHold(() => expiring);
    renderApp(PATH, { studentId: STUDENT });
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel application' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await vi.advanceTimersByTimeAsync(3_500);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(badgeText()).toBe('EXPIRED');
  });

  it('refuses a payment the server says came too late, even if this clock still showed time left', async () => {
    const backend = fakeBackend(morningShift);
    renderApp('/', { studentId: STUDENT });
    await userEvent.click(await applyFor(morningShift.name));
    await screen.findByRole('button', { name: 'Pay Admission Fee' });
    const [hold] = backend.holds;
    if (hold) hold.expiresAt = inSeconds(-1); // the server's clock got there first

    await userEvent.click(screen.getByRole('button', { name: 'Pay Admission Fee' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(messages.errors.HOLD_EXPIRED);
    expect(await main().findByText('EXPIRED')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pay Admission Fee' })).toBeDisabled();
  });
});

describe('confirm and cancel when something changed elsewhere', () => {
  it.each([
    ['paying a hold cancelled in another tab', 'confirm', 409, 'HOLD_CANCELLED', 'CANCELLED', messages.errors.HOLD_CANCELLED],
    ['cancelling a hold paid in another tab', 'cancel', 409, 'HOLD_ALREADY_CONFIRMED', 'CONFIRMED', messages.errors.HOLD_ALREADY_CONFIRMED],
    ['cancelling a hold that expired', 'cancel', 409, 'HOLD_EXPIRED', 'EXPIRED', messages.errors.HOLD_EXPIRED],
    ['paying a hold of another number', 'confirm', 403, 'HOLD_NOT_OWNED', 'HELD', messages.errors.HOLD_NOT_OWNED],
  ] as const)('%s', async (_label, action, status, code, after, text) => {
    let current = aHold();
    givenHold(() => current);
    const respond = () => {
      if (after !== 'HELD') current = { ...current, status: after, expiresAt: after === 'EXPIRED' ? inSeconds(-1) : current.expiresAt };
      return problem(status, code);
    };
    server.use(action === 'confirm' ? http.post('*/holds/:holdId/confirm', respond) : http.delete('*/holds/:holdId', respond));
    renderApp(PATH, { studentId: STUDENT });

    if (action === 'confirm') {
      await userEvent.click(await screen.findByRole('button', { name: 'Pay Admission Fee' }));
    } else {
      await userEvent.click(await screen.findByRole('button', { name: 'Cancel application' }));
      await userEvent.click(screen.getByRole('button', { name: 'Yes, cancel it' }));
    }

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
    await vi.waitFor(() => expect(badgeText()).toBe(after)); // the page shows the real state afterwards
  });

  it('treats a repeated payment as success (the API is idempotent)', async () => {
    let current = aHold();
    givenHold(() => current);
    let confirms = 0;
    server.use(
      http.post('*/holds/:holdId/confirm', () => {
        confirms += 1;
        current = { ...current, status: 'CONFIRMED' }; // already confirmed elsewhere: still 200
        return HttpResponse.json({ holdId: HOLD_ID, status: 'CONFIRMED' });
      }),
    );
    renderApp(PATH, { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Pay Admission Fee' }));

    expect(await main().findByText('CONFIRMED')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(confirms).toBe(1);
  });
});

describe('error boundary', () => {
  it('shows a friendly page instead of a blank screen when the server sends data the page cannot show', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    server.use(http.get('*/holds/:holdId', () => HttpResponse.json({ holdId: HOLD_ID, status: 42 })));

    renderApp(PATH, { studentId: STUDENT });

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(messages.errorBoundary.title);
    expect(within(alert).getByRole('button', { name: 'Reload' })).toBeInTheDocument();
    expect(within(alert).getByRole('link', { name: 'Go to classes' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument(); // header still usable
    expect(consoleError).toHaveBeenCalled(); // details kept for developers
    consoleError.mockRestore();
  });

  it('also catches crashes outside the router (the root boundary)', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    function Boom(): never {
      throw new Error('provider failed');
    }

    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByRole('alert')).toHaveTextContent(messages.errorBoundary.title);
    expect(screen.getByRole('link', { name: 'Go to classes' })).toHaveAttribute('href', '/');
    expect(consoleError.mock.calls.some((args) => args[0] === 'Render error caught by ErrorBoundary')).toBe(true);
    consoleError.mockRestore();
  });

  it('the Reload button reloads the page', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const reload = vi.fn();
    const original = window.location;
    Object.defineProperty(window, 'location', { configurable: true, value: { ...original, reload } });
    function Boom(): never {
      throw new Error('render failed');
    }
    try {
      render(
        <ErrorBoundary>
          <Boom />
        </ErrorBoundary>,
      );

      await userEvent.click(screen.getByRole('button', { name: 'Reload' }));

      expect(reload).toHaveBeenCalledOnce();
    } finally {
      Object.defineProperty(window, 'location', { configurable: true, value: original });
      vi.restoreAllMocks();
    }
  });
});
