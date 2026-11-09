import { useId, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { ApiError } from '../api/client';
import type { CreatedClass } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { useCreateClass } from '../hooks/queries';
import { messages } from '../i18n/messages';

const t = messages.createClass;
const MAX_NAME = 100;
const MIN_SEATS = 1;
const MAX_SEATS = 10_000;

interface FieldErrors {
  name?: string;
  totalSeats?: string;
}

/** Mirrors the server rules (API.md §4.1) so most mistakes never leave the browser. */
function validateClassForm(name: string, seats: string): FieldErrors {
  const errors: FieldErrors = {};
  const trimmed = name.trim();
  if (trimmed.length === 0) errors.name = t.nameRequired;
  else if (trimmed.length > MAX_NAME) errors.name = t.nameTooLong(MAX_NAME);

  const s = seats.trim();
  if (s === '') errors.totalSeats = t.seatsRequired;
  else if (!/^\d+$/.test(s)) errors.totalSeats = t.seatsWhole;
  else if (Number(s) < MIN_SEATS || Number(s) > MAX_SEATS) errors.totalSeats = t.seatsRange(MIN_SEATS, MAX_SEATS);
  return errors;
}

/** Field messages from the server: VALIDATION_FAILED `errors`, or a duplicate name. */
function serverFieldErrors(error: unknown): FieldErrors {
  if (!(error instanceof ApiError)) return {};
  if (error.code === 'CLASS_NAME_EXISTS') return { name: messages.errors.CLASS_NAME_EXISTS };
  if (error.code !== 'VALIDATION_FAILED') return {};
  const errors = error.problem.errors ?? {};
  return { name: errors.name?.[0], totalSeats: errors.totalSeats?.[0] };
}

/** Admin: Create Class (Design.md §4.2). Unprotected by design (no auth in this system). */
export function CreateClassPage() {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('');
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});
  const [created, setCreated] = useState<CreatedClass | null>(null);
  const create = useCreateClass();
  const nameRef = useRef<HTMLInputElement>(null);
  const nameId = useId();
  const seatsId = useId();

  const server = serverFieldErrors(create.error);
  const nameError = clientErrors.name ?? server.name;
  const seatsError = clientErrors.totalSeats ?? server.totalSeats;
  const showGeneralError = create.isError && !server.name && !server.totalSeats;

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    const errors = validateClassForm(name, seats);
    setClientErrors(errors);
    setCreated(null);
    if (errors.name || errors.totalSeats) return;

    create.mutate(
      { name: name.trim(), totalSeats: Number(seats.trim()) },
      {
        onSuccess: (cls) => {
          setCreated(cls);
          setName('');
          setSeats('');
          nameRef.current?.focus();
        },
      },
    );
  }

  return (
    <>
      <h1>{messages.pages.createClass}</h1>
      <p className="muted">{t.intro}</p>

      {created && (
        <div className="alert alert-success" role="status">
          <p>{t.created(created.name, created.totalSeats)}</p>
          <Link to="/">{t.viewClasses}</Link>
        </div>
      )}
      {showGeneralError && <ErrorAlert error={create.error} />}

      <form className="form" noValidate onSubmit={onSubmit}>
        <div className="field">
          <label htmlFor={nameId}>{t.nameLabel}</label>
          <input
            ref={nameRef}
            id={nameId}
            type="text"
            value={name}
            maxLength={150}
            placeholder={t.namePlaceholder}
            autoComplete="off"
            onChange={(e) => { setName(e.target.value); if (create.isError) create.reset(); }}
            aria-invalid={nameError ? true : undefined}
            aria-describedby={nameError ? `${nameId}-error` : undefined}
          />
          {nameError && (
            <p id={`${nameId}-error`} className="field-error" role="alert">
              {nameError}
            </p>
          )}
        </div>

        <div className="field">
          <label htmlFor={seatsId}>{t.seatsLabel}</label>
          <input
            id={seatsId}
            type="text"
            inputMode="numeric"
            value={seats}
            maxLength={6}
            placeholder="120"
            autoComplete="off"
            onChange={(e) => { setSeats(e.target.value); if (create.isError) create.reset(); }}
            aria-invalid={seatsError ? true : undefined}
            aria-describedby={seatsError ? `${seatsId}-error` : undefined}
          />
          {seatsError && (
            <p id={`${seatsId}-error`} className="field-error" role="alert">
              {seatsError}
            </p>
          )}
        </div>

        <button type="submit" disabled={create.isPending}>
          {create.isPending ? t.submitting : t.submit}
        </button>
      </form>
    </>
  );
}
