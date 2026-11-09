import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { CreateClassRequest } from '../api/types';
import { problem } from '../test/handlers';
import { renderApp } from '../test/render';
import { server } from '../test/server';

const nameInput = () => screen.getByLabelText('Class name');
const seatsInput = () => screen.getByLabelText('Total seats');
const submit = () => screen.getByRole('button', { name: 'Create class' });

/** Records POST /classes bodies; answers 201 like the backend (name trimmed). */
function acceptCreates() {
  const bodies: CreateClassRequest[] = [];
  server.use(
    http.post('*/classes', async ({ request }) => {
      const body = (await request.json()) as CreateClassRequest;
      bodies.push(body);
      return HttpResponse.json({ id: 'c-new', name: body.name, totalSeats: body.totalSeats }, { status: 201 });
    }),
  );
  return bodies;
}

async function fill(name: string, seats: string) {
  if (name) await userEvent.type(nameInput(), name);
  if (seats) await userEvent.type(seatsInput(), seats);
  await userEvent.click(submit());
}

describe('CreateClassPage', () => {
  it('creates a class, confirms it and clears the form', async () => {
    const bodies = acceptCreates();
    renderApp('/admin/classes/new');

    await fill('  Class 6 - Morning Shift ', '120');

    expect(await screen.findByRole('status')).toHaveTextContent('✓ "Class 6 - Morning Shift" created with 120 seats.');
    expect(screen.getByRole('link', { name: 'View classes' })).toHaveAttribute('href', '/');
    expect(bodies).toEqual([{ name: 'Class 6 - Morning Shift', totalSeats: 120 }]); // trimmed, JSON integer
    expect(nameInput()).toHaveValue('');
    expect(seatsInput()).toHaveValue('');
    expect(nameInput()).toHaveFocus();
  });

  it('validates before sending: both fields required', async () => {
    const bodies = acceptCreates();
    renderApp('/admin/classes/new');

    await userEvent.click(submit());

    expect(screen.getByText('Name is required.')).toBeInTheDocument();
    expect(screen.getByText('Total seats is required.')).toBeInTheDocument();
    expect(nameInput()).toHaveAttribute('aria-invalid', 'true');
    expect(nameInput()).toHaveAccessibleDescription('Name is required.');
    expect(bodies).toHaveLength(0);
  });

  it.each([
    ['0', 'Total seats must be between 1 and 10000.'],
    ['10001', 'Total seats must be between 1 and 10000.'],
    ['2.5', 'Total seats must be a whole number.'],
    ['abc', 'Total seats must be a whole number.'],
    ['-3', 'Total seats must be a whole number.'],
  ])('rejects %j seats with "%s"', async (seats, message) => {
    const bodies = acceptCreates();
    renderApp('/admin/classes/new');

    await fill('Class 7', seats);

    expect(screen.getByText(message)).toBeInTheDocument();
    expect(bodies).toHaveLength(0);
  });

  it('rejects a name longer than 100 characters (after trimming) and blank names', async () => {
    acceptCreates();
    renderApp('/admin/classes/new');

    await fill('x'.repeat(101), '5');
    expect(screen.getByText('Name must be at most 100 characters.')).toBeInTheDocument();

    await userEvent.clear(nameInput());
    await userEvent.type(nameInput(), '    ');
    await userEvent.click(submit());
    expect(screen.getByText('Name is required.')).toBeInTheDocument();
  });

  it('shows a duplicate name from the server next to the name field', async () => {
    server.use(http.post('*/classes', () => problem(409, 'CLASS_NAME_EXISTS')));
    renderApp('/admin/classes/new');

    await fill('Class 6 - Morning Shift', '120');

    const error = await screen.findByText(/A class with this name already exists/);
    expect(nameInput()).toHaveAccessibleDescription(error.textContent ?? '');
    expect(nameInput()).toHaveValue('Class 6 - Morning Shift'); // nothing lost; the admin can edit
  });

  it('shows server field errors under the matching field', async () => {
    server.use(
      http.post('*/classes', () =>
        problem(400, 'VALIDATION_FAILED', { errors: { totalSeats: ['Total seats must be between 1 and 10000.'] } }),
      ),
    );
    renderApp('/admin/classes/new');

    await fill('Class 8', '50');

    expect(await screen.findByText('Total seats must be between 1 and 10000.')).toBeInTheDocument();
    expect(seatsInput()).toHaveAttribute('aria-invalid', 'true');
  });

  it('clears a server error as soon as the admin edits the form', async () => {
    server.use(http.post('*/classes', () => problem(409, 'CLASS_NAME_EXISTS')));
    renderApp('/admin/classes/new');
    await fill('Dup', '5');
    await screen.findByText(/already exists/);

    await userEvent.type(nameInput(), ' 2');

    expect(screen.queryByText(/already exists/)).not.toBeInTheDocument();
  });

  it('explains connection problems in plain language', async () => {
    server.use(http.post('*/classes', () => HttpResponse.error()));
    renderApp('/admin/classes/new');

    await fill('Class 9', '30');

    expect(await screen.findByRole('alert')).toHaveTextContent(/Can't reach the server/);
  });

  it('disables the button while creating', async () => {
    server.use(http.post('*/classes', async () => { await delay('infinite'); return HttpResponse.json({}); }));
    renderApp('/admin/classes/new');

    await fill('Class 10', '30');

    await waitFor(() => expect(screen.getByRole('button', { name: 'Creating…' })).toBeDisabled());
  });

  it('new classes appear in the class list straight away', async () => {
    let classes: unknown[] = [];
    server.use(
      http.get('*/classes', () => HttpResponse.json(classes)),
      http.post('*/classes', () => {
        classes = [{ id: 'c-new', name: 'Class 11', totalSeats: 30, heldSeats: 0, confirmedSeats: 0, availableSeats: 30 }];
        return HttpResponse.json({ id: 'c-new', name: 'Class 11', totalSeats: 30 }, { status: 201 });
      }),
    );
    renderApp('/admin/classes/new');

    await fill('Class 11', '30');
    await userEvent.click(await screen.findByRole('link', { name: 'View classes' }));

    expect(await screen.findByRole('rowheader', { name: 'Class 11' })).toBeInTheDocument();
  });
});
