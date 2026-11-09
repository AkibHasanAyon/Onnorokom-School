import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { StudentProvider } from './StudentContext';
import { STORAGE_KEY } from './context';
import { useStudent } from './useStudent';
import { StudentIdBar } from './StudentIdBar';

const VALID = '20152691234567890';

/** Shows the identity the rest of the app would use. */
function CurrentIdentity() {
  const { studentId } = useStudent();
  return <output aria-label="identity">{studentId ?? 'none'}</output>;
}

function renderBar() {
  return render(
    <StudentProvider>
      <StudentIdBar />
      <CurrentIdentity />
    </StudentProvider>,
  );
}

const input = () => screen.getByLabelText('Birth Registration No.');
const identity = () => screen.getByLabelText('identity');

describe('StudentIdBar', () => {
  it('starts empty with a hint when nothing is saved', () => {
    renderBar();

    expect(input()).toHaveValue('');
    expect(input()).toHaveAttribute('inputmode', 'numeric');
    expect(screen.getByText(/Enter the student's birth registration number/)).toBeInTheDocument();
    expect(identity()).toHaveTextContent('none');
  });

  it('saves a valid 17-digit number to localStorage and uses it as the identity', async () => {
    renderBar();

    await userEvent.type(input(), VALID);

    expect(screen.getByText('✓ Saved')).toBeInTheDocument();
    expect(localStorage.getItem(STORAGE_KEY)).toBe(VALID);
    expect(identity()).toHaveTextContent(VALID);
    expect(input()).not.toHaveAttribute('aria-invalid');
  });

  it('shows how many digits were entered and saves nothing while invalid', async () => {
    renderBar();

    await userEvent.type(input(), '201526912345678');

    expect(screen.getByRole('alert')).toHaveTextContent('Must be exactly 17 digits (you entered 15).');
    expect(input()).toHaveAttribute('aria-invalid', 'true');
    expect(input()).toHaveAccessibleDescription('Must be exactly 17 digits (you entered 15).');
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    expect(identity()).toHaveTextContent('none');
  });

  it('rejects letters with a clear message', async () => {
    renderBar();

    await userEvent.type(input(), '2015abc');

    expect(screen.getByRole('alert')).toHaveTextContent('Use digits only (0–9 or ০–৯).');
  });

  it('accepts Bengali digits, spaces and dashes and shows the normalized number', async () => {
    renderBar();

    await userEvent.type(input(), '২০১৫-২৬৯১ ২৩৪৫৬৭৮৯০');

    expect(input()).toHaveValue(VALID);
    expect(localStorage.getItem(STORAGE_KEY)).toBe(VALID);
  });

  it('restores the saved number after a reload (R11)', () => {
    localStorage.setItem(STORAGE_KEY, VALID);

    renderBar();

    expect(input()).toHaveValue(VALID);
    expect(screen.getByText('✓ Saved')).toBeInTheDocument();
    expect(identity()).toHaveTextContent(VALID);
  });

  it('ignores a corrupted stored value', () => {
    localStorage.setItem(STORAGE_KEY, 'not-an-id');

    renderBar();

    expect(input()).toHaveValue('');
    expect(identity()).toHaveTextContent('none');
  });

  it('"Change" forgets the number (shared computers) and focuses the box', async () => {
    localStorage.setItem(STORAGE_KEY, VALID);
    renderBar();

    await userEvent.click(screen.getByRole('button', { name: 'Change' }));

    expect(input()).toHaveValue('');
    expect(input()).toHaveFocus();
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    expect(identity()).toHaveTextContent('none');
  });

  it('editing a saved number into an invalid one removes the identity', async () => {
    localStorage.setItem(STORAGE_KEY, VALID);
    renderBar();

    await userEvent.type(input(), '{Backspace}');

    expect(identity()).toHaveTextContent('none');
    expect(screen.getByRole('alert')).toHaveTextContent('you entered 16');
  });

  it('switching to another valid number updates the identity and storage', async () => {
    localStorage.setItem(STORAGE_KEY, VALID);
    renderBar();

    await userEvent.clear(input());
    await userEvent.type(input(), '20152691234567891');

    expect(identity()).toHaveTextContent('20152691234567891');
    expect(localStorage.getItem(STORAGE_KEY)).toBe('20152691234567891');
  });
});
