import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { ClassSummary } from '../api/types';
import { messages } from '../i18n/messages';
import { STORAGE_KEY } from '../student/context';
import { givenClasses, givenMyHoldsByStudent, recordRequests } from '../test/api';
import { aClass, aMyHold, inMinutes, morningShift, OTHER_STUDENT, STUDENT } from '../test/fixtures';
import { problem } from '../test/handlers';
import { renderApp } from '../test/render';
import { server } from '../test/server';

/** Testing_QA.md §5: student ID, class polling and apply errors — across the ID bar and the class list. */

const BENGALI_STUDENT = '২০১৫২৬৯১২৩৪৫৬৭৮৯০'; // = STUDENT
const applyButton = () => screen.findByRole('button', { name: 'Apply for Class 6 - Morning Shift' });
const idBox = () => screen.getByLabelText('Birth Registration No.');
const morningRow = () => screen.getByRole('row', { name: /Morning Shift/ });

function polledClasses(initial: ClassSummary[]) {
  let current = initial;
  let calls = 0;
  server.use(
    http.get('*/classes', () => {
      calls += 1;
      return HttpResponse.json(current);
    }),
  );
  return {
    set: (next: ClassSummary[]) => {
      current = next;
    },
    calls: () => calls,
  };
}

describe('student ID and the class list', () => {
  it('sends a number typed in Bengali digits as ASCII X-Student-Id when applying', async () => {
    givenClasses(morningShift);
    const applies = recordRequests('post', '*/classes/:classId/holds', () => problem(409, 'NO_SEATS_AVAILABLE'));
    renderApp('/');

    await userEvent.type(idBox(), BENGALI_STUDENT);
    await userEvent.click(await applyButton());

    await screen.findByRole('alert');
    expect(applies).toEqual([{ method: 'POST', path: `/classes/${morningShift.id}/holds`, studentId: STUDENT }]);
  });

  it('enables Apply once a valid number is entered and disables it again after "Change"', async () => {
    givenClasses(morningShift);
    renderApp('/');
    const disabled = await within(await screen.findByRole('row', { name: /Morning Shift/ })).findByRole('button', { name: 'Apply' });
    expect(disabled).toBeDisabled();

    await userEvent.type(idBox(), STUDENT);
    expect(await applyButton()).toBeEnabled();

    await userEvent.click(screen.getByRole('button', { name: 'Change' }));
    expect(within(morningRow()).getByRole('button', { name: 'Apply' })).toBeDisabled();
    expect(screen.getByText(messages.classList.needStudentId)).toBeInTheDocument();
  });

  it('never applies with an incomplete number', async () => {
    givenClasses(morningShift);
    const applies = recordRequests('post', '*/classes/:classId/holds', () => HttpResponse.json({}, { status: 201 }));
    renderApp('/');

    await userEvent.type(idBox(), STUDENT.slice(0, 16));

    expect(screen.getByText('Must be exactly 17 digits (you entered 16).')).toBeInTheDocument();
    await userEvent.click(within(morningRow()).getByRole('button', { name: 'Apply' }));
    expect(applies).toEqual([]);
  });

  it("switching to another number updates the Apply buttons to that student's applications", async () => {
    givenClasses(morningShift);
    givenMyHoldsByStudent({ [STUDENT]: [aMyHold({ holdId: 'h-a', expiresAt: inMinutes(5) })] });
    renderApp('/', { studentId: STUDENT });
    expect(await within(await screen.findByRole('row', { name: /Morning Shift/ })).findByRole('link', { name: 'View application' }))
      .toHaveAttribute('href', '/applications/h-a');

    await userEvent.click(screen.getByRole('button', { name: 'Change' }));
    await userEvent.type(idBox(), OTHER_STUDENT);

    expect(await applyButton()).toBeEnabled();
    expect(within(morningRow()).queryByRole('link', { name: 'View application' })).not.toBeInTheDocument();
  });

  it('uses the number saved on this device without typing it again (R11)', async () => {
    localStorage.setItem(STORAGE_KEY, STUDENT);
    givenClasses(morningShift);

    renderApp('/');

    expect(idBox()).toHaveValue(STUDENT);
    expect(await applyButton()).toBeEnabled();
  });

  it('follows a number changed in another tab, in the box as well as for Apply', async () => {
    givenClasses(morningShift);
    renderApp('/', { studentId: STUDENT });
    await applyButton();

    act(() => {
      localStorage.setItem(STORAGE_KEY, OTHER_STUDENT);
      window.dispatchEvent(new StorageEvent('storage', { key: STORAGE_KEY, newValue: OTHER_STUDENT }));
    });
    expect(idBox()).toHaveValue(OTHER_STUDENT);

    act(() => {
      localStorage.removeItem(STORAGE_KEY);
      window.dispatchEvent(new StorageEvent('storage', { key: STORAGE_KEY, newValue: null }));
    });
    expect(idBox()).toHaveValue('');
    expect(within(morningRow()).getByRole('button', { name: 'Apply' })).toBeDisabled();
  });
});

describe('class list polling (R10)', () => {
  it('updates held, confirmed and available seats and turns Apply into "Full" when the class fills up', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const classes = polledClasses([morningShift]);
    renderApp('/', { studentId: STUDENT });
    expect(await applyButton()).toBeEnabled();

    classes.set([{ ...morningShift, heldSeats: 30, confirmedSeats: 90, availableSeats: 0 }]);
    await vi.advanceTimersByTimeAsync(3_000);

    const cells = await within(morningRow()).findAllByRole('cell');
    await vi.waitFor(() => expect(cells.slice(0, 4).map((c) => c.textContent)).toEqual(['120', '30', '90', '0']));
    expect(within(morningRow()).getByRole('button', { name: 'Full' })).toBeDisabled();
  });

  it('turns "Full" back into Apply when a seat is released', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const classes = polledClasses([{ ...morningShift, heldSeats: 40, confirmedSeats: 80, availableSeats: 0 }]);
    renderApp('/', { studentId: STUDENT });
    expect(await within(await screen.findByRole('row', { name: /Morning Shift/ })).findByRole('button', { name: 'Full' })).toBeDisabled();

    classes.set([{ ...morningShift, heldSeats: 39, confirmedSeats: 80, availableSeats: 1 }]);
    await vi.advanceTimersByTimeAsync(3_000);

    expect(await applyButton()).toBeEnabled();
  });

  it('shows a class created elsewhere on the next refresh', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const classes = polledClasses([morningShift]);
    renderApp('/');
    await screen.findByRole('row', { name: /Morning Shift/ });

    classes.set([morningShift, aClass({ id: 'c-new', name: 'Class 9 - Evening Shift' })]);
    await vi.advanceTimersByTimeAsync(3_000);

    expect(await screen.findByRole('row', { name: /Evening Shift/ })).toBeInTheDocument();
  });

  it('keeps polling after a failed refresh and shows the new numbers when the server is back', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    givenClasses(morningShift);
    renderApp('/');
    await screen.findByRole('cell', { name: '25' });

    server.use(http.get('*/classes', () => HttpResponse.error()));
    await vi.advanceTimersByTimeAsync(3_000);
    givenClasses({ ...morningShift, availableSeats: 7 });
    await vi.advanceTimersByTimeAsync(3_000);

    expect(await screen.findByRole('cell', { name: '7' })).toBeInTheDocument();
  });

  it('refreshes the seat numbers right after a rejected application, without waiting for the next poll', async () => {
    const classes = polledClasses([morningShift]);
    server.use(
      http.post('*/classes/:classId/holds', () => {
        classes.set([{ ...morningShift, heldSeats: 40, availableSeats: 0 }]); // the last seats went to others
        return problem(409, 'NO_SEATS_AVAILABLE');
      }),
    );
    renderApp('/', { studentId: STUDENT });
    const button = await applyButton();
    const callsBefore = classes.calls();

    await userEvent.click(button);
    await screen.findByRole('alert');

    // findBy gives up after 1 s, well before the 3 s poll: only the immediate refetch can make this pass.
    expect(await within(morningRow()).findByRole('button', { name: 'Full' })).toBeDisabled();
    expect(classes.calls()).toBe(callsBefore + 1);
  });
});

describe('apply errors are sentences, never codes (R12)', () => {
  const t = messages.errors;

  it.each([
    ['409 NO_SEATS_AVAILABLE', () => problem(409, 'NO_SEATS_AVAILABLE'), t.NO_SEATS_AVAILABLE],
    ['409 ALREADY_ADMITTED', () => problem(409, 'ALREADY_ADMITTED', { existingHoldId: 'h-done' }), t.ALREADY_ADMITTED],
    ['400 INVALID_STUDENT_ID', () => problem(400, 'INVALID_STUDENT_ID'), t.INVALID_STUDENT_ID],
    ['404 CLASS_NOT_FOUND', () => problem(404, 'CLASS_NOT_FOUND'), t.CLASS_NOT_FOUND],
    ['503 SERVICE_UNAVAILABLE', () => problem(503, 'SERVICE_UNAVAILABLE'), t.SERVICE_UNAVAILABLE],
    ['500 without a code', () => new HttpResponse('<html>oops</html>', { status: 500 }), t.server],
    ['502 from a proxy', () => new HttpResponse('Bad Gateway', { status: 502, headers: { 'Content-Type': 'text/plain' } }), t.server],
    ['an unknown 4xx code', () => problem(422, 'SOMETHING_NEW'), t.unknown],
    ['a network failure', () => HttpResponse.error(), t.network],
  ])('%s', async (_label, respond, text) => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {}); // unknown codes are logged
    givenClasses(morningShift);
    server.use(http.post('*/classes/:classId/holds', respond));
    renderApp('/', { studentId: STUDENT });

    await userEvent.click(await applyButton());

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(text);
    expect(alert.textContent).not.toMatch(/[A-Z]{2,}_[A-Z_]+|HTTP_\d+|\b[45]\d\d\b/); // no raw codes or statuses
    expect(await applyButton()).toBeEnabled(); // the parent can try again
    consoleError.mockRestore();
  });

  it('links to the admitted application on 409 ALREADY_ADMITTED', async () => {
    givenClasses(morningShift);
    server.use(http.post('*/classes/:classId/holds', () => problem(409, 'ALREADY_ADMITTED', { existingHoldId: 'h-done' })));
    renderApp('/', { studentId: STUDENT });

    await userEvent.click(await applyButton());

    expect(within(await screen.findByRole('alert')).getByRole('link', { name: 'View' })).toHaveAttribute('href', '/applications/h-done');
  });

  it('explains a server that takes too long', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    givenClasses(morningShift);
    server.use(
      http.post('*/classes/:classId/holds', async () => {
        await delay('infinite');
        return HttpResponse.json({});
      }),
    );
    renderApp('/', { studentId: STUDENT });

    await userEvent.click(await applyButton());
    await vi.advanceTimersByTimeAsync(10_000);

    expect(await screen.findByRole('alert')).toHaveTextContent(t.timeout);
  });

  it('replaces the previous error when the next attempt fails differently', async () => {
    givenClasses(morningShift);
    server.use(http.post('*/classes/:classId/holds', () => problem(409, 'NO_SEATS_AVAILABLE')));
    renderApp('/', { studentId: STUDENT });
    await userEvent.click(await applyButton());
    expect(await screen.findByRole('alert')).toHaveTextContent(t.NO_SEATS_AVAILABLE);

    server.use(http.post('*/classes/:classId/holds', () => HttpResponse.error()));
    await userEvent.click(await applyButton());

    await vi.waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(t.network));
    expect(screen.getAllByRole('alert')).toHaveLength(1);
  });
});
