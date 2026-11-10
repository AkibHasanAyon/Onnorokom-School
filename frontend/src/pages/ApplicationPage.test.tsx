import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { reportServerTime } from '../api/serverClock';
import type { HoldDetails, HoldStatus } from '../api/types';
import { givenHold } from '../test/api';
import { aHold, asMyHold, HOLD_ID, OTHER_STUDENT, STUDENT } from '../test/fixtures';
import { problem } from '../test/handlers';
import { renderApp } from '../test/render';
import { server } from '../test/server';

const PATH = `/applications/${HOLD_ID}`;

const hold = (status: HoldStatus, expiresInSeconds = 600): HoldDetails =>
  aHold({ status, expiresAt: new Date(Date.now() + expiresInSeconds * 1000).toISOString() });

const main = () => within(screen.getByRole('main'));
const badge = () => main().getByText(/^(HELD|CONFIRMED|EXPIRED|CANCELLED)$/);

describe('ApplicationPage', () => {
  it('shows the class, the HELD badge, a live countdown and both actions', async () => {
    const h = hold('HELD', 462);
    givenHold(() => h);

    renderApp(PATH, { studentId: STUDENT });

    expect(await screen.findByRole('heading', { level: 1, name: 'Application — Class 6 - Morning Shift' })).toBeInTheDocument();
    expect(badge()).toHaveTextContent('HELD');
    expect(main().getByText('Seat held — pay before the timer ends.')).toBeInTheDocument();
    expect(screen.getByTestId('countdown')).toHaveTextContent(/^07:4[12]$/);
    expect(main().getByText(/^\(expires at /)).toHaveAttribute('title', h.expiresAt);
    expect(await screen.findByRole('button', { name: 'Pay Admission Fee' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Cancel application' })).toBeEnabled();
    expect(main().getByText(/Paying is simulated/)).toBeInTheDocument();
  });

  it('counts down and switches to EXPIRED with Pay disabled at 0, without a reload (R9)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const h = hold('HELD', 2); // the server keeps saying HELD: the page must not wait for it
    let holdFetches = 0;
    server.use(
      http.get(`*/holds/${HOLD_ID}`, () => {
        holdFetches += 1;
        return HttpResponse.json(h);
      }),
      http.get('*/me/holds', () => HttpResponse.json([asMyHold(h)])),
    );
    renderApp(PATH, { studentId: STUDENT });
    expect(await screen.findByRole('button', { name: 'Pay Admission Fee' })).toBeEnabled();
    const fetchesBefore = holdFetches;

    await vi.advanceTimersByTimeAsync(2_500);

    expect(badge()).toHaveTextContent('EXPIRED');
    expect(main().getByText(/^Time ran out\. The seat was released\. You can apply again/)).toBeInTheDocument();
    const pay = screen.getByRole('button', { name: 'Pay Admission Fee' });
    expect(pay).toBeDisabled();
    expect(pay).toHaveAccessibleDescription('This application has expired, so it can no longer be paid.');
    expect(screen.queryByRole('button', { name: 'Cancel application' })).not.toBeInTheDocument();
    expect(screen.queryByTestId('countdown')).not.toBeInTheDocument();
    expect(main().getByRole('link', { name: 'Back to classes' })).toHaveAttribute('href', '/');
    await waitFor(() => expect(holdFetches).toBeGreaterThan(fetchesBefore)); // asks the server at 0
  });

  it('warns in the last minute and announces only at one minute and at expiry', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const h = hold('HELD', 62);
    givenHold(() => h);
    renderApp(PATH, { studentId: STUDENT });
    await screen.findByTestId('countdown');
    const live = document.querySelector('[aria-live="polite"]');
    expect(main().queryByText('Hurry!')).not.toBeInTheDocument();
    expect(live).toHaveTextContent(/^$/);

    await vi.advanceTimersByTimeAsync(3_000);

    expect(main().getByText('Hurry!')).toBeInTheDocument();
    expect(screen.getByTestId('countdown')).toHaveClass('countdown-urgent');
    expect(live).toHaveTextContent('One minute left to pay.');
    const textAtOneMinute = live?.textContent;
    await vi.advanceTimersByTimeAsync(5_000);
    expect(live?.textContent).toBe(textAtOneMinute); // unchanged while ticking

    await vi.advanceTimersByTimeAsync(60_000);
    expect(live).toHaveTextContent('Time ran out. The seat was released.');
  });

  it('uses the server clock, not the browser clock, for the countdown', async () => {
    const h = hold('HELD', 600);
    givenHold(() => h);
    reportServerTime(new Date(Date.now() + 5 * 60_000).toISOString()); // browser clock 5 min slow

    renderApp(PATH, { studentId: STUDENT });

    expect(await screen.findByTestId('countdown')).toHaveTextContent(/^0[45]:\d\d$/);
  });

  it('pays the fee and shows CONFIRMED', async () => {
    let current = hold('HELD');
    let sentStudent: string | null = null;
    givenHold(() => current);
    server.use(
      http.post(`*/holds/${HOLD_ID}/confirm`, ({ request }) => {
        sentStudent = request.headers.get('X-Student-Id');
        current = { ...current, status: 'CONFIRMED' };
        return HttpResponse.json({ holdId: HOLD_ID, status: 'CONFIRMED' });
      }),
    );
    renderApp(PATH, { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Pay Admission Fee' }));

    expect(await main().findByText('CONFIRMED')).toBeInTheDocument();
    expect(main().getByText('Admission confirmed. 🎉')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Payment received');
    expect(screen.queryByRole('button', { name: 'Pay Admission Fee' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancel application' })).not.toBeInTheDocument();
    expect(sentStudent).toBe(STUDENT);
  });

  it('shows "Paying…" and blocks both buttons while paying', async () => {
    givenHold(() => hold('HELD'));
    server.use(
      http.post(`*/holds/${HOLD_ID}/confirm`, async () => {
        await delay('infinite');
        return HttpResponse.json({});
      }),
    );
    renderApp(PATH, { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Pay Admission Fee' }));

    expect(await screen.findByRole('button', { name: 'Paying…' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel application' })).toBeDisabled();
  });

  it('explains a "too late" payment and flips to EXPIRED (server 409 HOLD_EXPIRED)', async () => {
    let current = hold('HELD');
    givenHold(() => current);
    server.use(
      http.post(`*/holds/${HOLD_ID}/confirm`, () => {
        current = { ...current, status: 'EXPIRED' };
        return problem(409, 'HOLD_EXPIRED');
      }),
    );
    renderApp(PATH, { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Pay Admission Fee' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "This application has expired, so it can't be paid or cancelled. You can apply again if seats are available.",
    );
    expect(await main().findByText('EXPIRED')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pay Admission Fee' })).toBeDisabled();
  });

  it('shows a friendly message when paying fails because the server is unreachable', async () => {
    givenHold(() => hold('HELD'));
    server.use(http.post(`*/holds/${HOLD_ID}/confirm`, () => HttpResponse.error()));
    renderApp(PATH, { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Pay Admission Fee' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("Can't reach the server.");
    expect(screen.getByRole('button', { name: 'Pay Admission Fee' })).toBeEnabled(); // can try again
  });

  it('asks before cancelling; "Keep my application" and Esc close without cancelling', async () => {
    let deletes = 0;
    givenHold(() => hold('HELD'));
    server.use(
      http.delete(`*/holds/${HOLD_ID}`, () => {
        deletes += 1;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp(PATH, { studentId: STUDENT });
    const cancelButton = await screen.findByRole('button', { name: 'Cancel application' });

    await userEvent.click(cancelButton);
    const dialog = screen.getByRole('dialog', { name: 'Cancel this application?' });
    expect(dialog).toHaveAccessibleDescription('Your seat will be released immediately.');
    expect(within(dialog).getByRole('button', { name: 'Keep my application' })).toHaveFocus();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Keep my application' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(cancelButton).toHaveFocus();

    await userEvent.click(cancelButton);
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(deletes).toBe(0);
  });

  it('keeps keyboard focus inside the cancel dialog', async () => {
    givenHold(() => hold('HELD'));
    renderApp(PATH, { studentId: STUDENT });
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel application' }));
    const dialog = screen.getByRole('dialog');
    const yes = within(dialog).getByRole('button', { name: 'Yes, cancel it' });
    const no = within(dialog).getByRole('button', { name: 'Keep my application' });

    await userEvent.tab();
    expect(yes).toHaveFocus();
    await userEvent.tab({ shift: true });
    expect(no).toHaveFocus();
  });

  it('cancels after confirmation and shows CANCELLED', async () => {
    let current = hold('HELD');
    let sentStudent: string | null = null;
    givenHold(() => current);
    server.use(
      http.delete(`*/holds/${HOLD_ID}`, ({ request }) => {
        sentStudent = request.headers.get('X-Student-Id');
        current = { ...current, status: 'CANCELLED' };
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp(PATH, { studentId: STUDENT });

    await userEvent.click(await screen.findByRole('button', { name: 'Cancel application' }));
    await userEvent.click(screen.getByRole('button', { name: 'Yes, cancel it' }));

    expect(await main().findByText('CANCELLED')).toBeInTheDocument();
    expect(main().getByText('You cancelled this application.')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('the seat was released');
    expect(main().getByRole('link', { name: 'Back to classes' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Pay Admission Fee' })).not.toBeInTheDocument();
    expect(sentStudent).toBe(STUDENT);
  });

  it('shows a confirmed admission without Pay or Cancel', async () => {
    givenHold(() => hold('CONFIRMED'));

    renderApp(PATH, { studentId: STUDENT });

    expect(await main().findByText('CONFIRMED')).toBeInTheDocument();
    expect(main().getByText('Admission confirmed. 🎉')).toBeInTheDocument();
    expect(main().queryByRole('button')).toBeNull();
    expect(screen.queryByTestId('countdown')).not.toBeInTheDocument();
  });

  it('shows a stored EXPIRED application with Pay disabled and a way back', async () => {
    givenHold(() => hold('EXPIRED', -60));

    renderApp(PATH, { studentId: STUDENT });

    expect(await main().findByText('EXPIRED')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Pay Admission Fee' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Cancel application' })).not.toBeInTheDocument();
    expect(main().getByRole('link', { name: 'Back to classes' })).toBeInTheDocument();
  });

  it('shows a cancelled application with a way back and no actions', async () => {
    givenHold(() => hold('CANCELLED'));

    renderApp(PATH, { studentId: STUDENT });

    expect(await main().findByText('You cancelled this application.')).toBeInTheDocument();
    expect(main().getByRole('link', { name: 'Back to classes' })).toBeInTheDocument();
    expect(main().queryByRole('button')).toBeNull();
  });

  it('is read-only when the application belongs to a different number', async () => {
    server.use(
      http.get(`*/holds/${HOLD_ID}`, () => HttpResponse.json(hold('HELD'))),
      http.get('*/me/holds', () => HttpResponse.json([])),
    );

    renderApp(PATH, { studentId: OTHER_STUDENT });

    expect(await main().findByText('This application belongs to a different birth registration number.')).toBeInTheDocument();
    expect(badge()).toHaveTextContent('HELD');
    expect(main().queryByRole('button')).toBeNull();
  });

  it('asks for the number before paying when none is saved', async () => {
    server.use(http.get(`*/holds/${HOLD_ID}`, () => HttpResponse.json(hold('HELD'))));

    renderApp(PATH);

    expect(await main().findByText(/Enter the birth registration number this application was made for/)).toBeInTheDocument();
    expect(main().queryByRole('button')).toBeNull();
  });

  it('notices a payment made elsewhere within 5 seconds', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let current = hold('HELD');
    givenHold(() => current);
    renderApp(PATH, { studentId: STUDENT });
    await screen.findByRole('button', { name: 'Pay Admission Fee' });

    current = { ...current, status: 'CONFIRMED' };
    await vi.advanceTimersByTimeAsync(5_000);

    expect(await main().findByText('CONFIRMED')).toBeInTheDocument();
  });

  it('shows a friendly message for an unknown application', async () => {
    renderApp(PATH, { studentId: STUDENT }); // default handler: 404 HOLD_NOT_FOUND

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "We couldn't find this application. It may have been entered incorrectly.",
    );
    expect(main().getByRole('link', { name: 'Back to classes' })).toHaveAttribute('href', '/');
  });

  it('shows a friendly message and Retry when the server is unreachable', async () => {
    server.use(http.get(`*/holds/${HOLD_ID}`, () => HttpResponse.error()));
    renderApp(PATH, { studentId: STUDENT });

    expect(await screen.findByRole('alert')).toHaveTextContent("Can't reach the server.");

    givenHold(() => hold('HELD'));
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await main().findByText('HELD')).toBeInTheDocument();
  });
});
