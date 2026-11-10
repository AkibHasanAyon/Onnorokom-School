import { useId, useRef, useState } from 'react';
import { messages } from '../i18n/messages';
import { useStudent } from './useStudent';
import { digitCount, isValidStudentId, normalizeStudentId, STUDENT_ID_LENGTH } from './studentId';

/**
 * The identity box at the top of every page (Design.md §3). What's in the box is the identity:
 * a valid number is saved immediately (and persisted, R11); an empty or invalid box means "no student",
 * so nothing is ever done for a number the parent can't see on screen.
 */
export function StudentIdBar() {
  const { studentId, saveStudentId, clearStudentId } = useStudent();
  const [text, setText] = useState(studentId ?? '');
  const inputRef = useRef<HTMLInputElement>(null);
  const inputId = useId();
  const hintId = useId();
  const t = messages.studentId;

  const normalized = normalizeStudentId(text);
  const valid = isValidStudentId(normalized);
  const error = text.trim() === '' || valid ? null : validationMessage(normalized);
  const saved = valid && studentId === normalized;

  // The identity changed somewhere else (another tab): show it, so the box always matches who we act for.
  // Changes made in this box already match; adjusting state during render avoids a flash of the old value.
  const [seenStudentId, setSeenStudentId] = useState(studentId);
  if (studentId !== seenStudentId) {
    setSeenStudentId(studentId);
    if (studentId !== (valid ? normalized : null)) setText(studentId ?? '');
  }

  function onChange(value: string) {
    setText(value);
    const next = normalizeStudentId(value);
    if (isValidStudentId(next)) {
      saveStudentId(next);
      setText(next); // show the number as it will be sent (Bengali digits → 0–9)
    } else if (studentId !== null) {
      clearStudentId();
    }
  }

  function onChangeClick() {
    clearStudentId();
    setText('');
    inputRef.current?.focus();
  }

  return (
    <div className="student-bar">
      <label htmlFor={inputId}>{t.label}</label>
      <div className="student-bar-row">
        <input
          ref={inputRef}
          id={inputId}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          spellCheck={false}
          maxLength={25}
          placeholder={t.placeholder}
          value={text}
          onChange={(e) => onChange(e.target.value)}
          aria-invalid={error ? true : undefined}
          aria-describedby={hintId}
        />
        {saved && (
          <>
            <span className="student-bar-saved">{t.saved}</span>
            <button type="button" className="secondary" onClick={onChangeClick}>
              {t.change}
            </button>
          </>
        )}
      </div>
      <p id={hintId} className={error ? 'field-error' : 'muted student-bar-hint'} role={error ? 'alert' : undefined}>
        {error ?? (saved ? t.savedHint : t.helper)}
      </p>
    </div>
  );
}

function validationMessage(normalized: string): string {
  if (/[^0-9]/.test(normalized)) return messages.studentId.digitsOnly;
  return messages.studentId.wrongLength(STUDENT_ID_LENGTH, digitCount(normalized));
}
