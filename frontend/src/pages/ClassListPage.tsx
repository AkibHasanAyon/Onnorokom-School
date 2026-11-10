import { useId } from 'react';
import { Link, useNavigate } from 'react-router';
import { ApiError } from '../api/client';
import { serverNow } from '../api/serverClock';
import type { ClassSummary, MyHold } from '../api/types';
import { ErrorAlert } from '../components/ErrorAlert';
import { FreshnessNote } from '../components/FreshnessNote';
import { useClasses, useCreateHold, useMyHolds } from '../hooks/queries';
import { useNow } from '../hooks/useNow';
import { messages } from '../i18n/messages';
import { useStudent } from '../student/useStudent';

const t = messages.classList;

/** This student's relevant hold per class: CONFIRMED wins, else a HELD hold that hasn't expired yet. */
function holdsByClass(holds: MyHold[] | undefined, nowMs: number): Map<string, MyHold> {
  const map = new Map<string, MyHold>();
  for (const hold of holds ?? []) {
    const active = hold.status === 'HELD' && Date.parse(hold.expiresAt) > nowMs;
    if (hold.status === 'CONFIRMED' || (active && map.get(hold.classId)?.status !== 'CONFIRMED')) {
      map.set(hold.classId, hold);
    }
  }
  return map;
}

/** Class List (Design.md §4.1): live seat counts (R10) and the Apply flow. */
export function ClassListPage() {
  const { studentId } = useStudent();
  const classes = useClasses();
  const myHolds = useMyHolds(studentId);
  const apply = useCreateHold(studentId);
  const navigate = useNavigate();
  useNow(1_000); // re-check which holds are still active as time passes
  const hintId = useId();

  const mine = holdsByClass(studentId ? myHolds.data : undefined, serverNow());

  function onApply(classId: string) {
    apply.mutate(classId, {
      onSuccess: (hold) => navigate(`/applications/${hold.holdId}`),
    });
  }

  return (
    <>
      <h1>{messages.pages.classes}</h1>
      <p className="muted">
        {t.autoRefresh}{' '}
        <FreshnessNote updatedAt={classes.dataUpdatedAt} stale={classes.isError} />
      </p>
      {!studentId && (
        <p id={hintId} className="notice">
          {t.needStudentId}
        </p>
      )}

      {apply.isError && <ErrorAlert error={apply.error} action={existingHoldLink(apply.error)} />}

      {classes.isPending ? (
        <p role="status">{t.loading}</p>
      ) : classes.isError && !classes.data ? (
        <ErrorAlert error={classes.error} onRetry={() => void classes.refetch()} />
      ) : classes.data.length === 0 ? (
        <p>
          {t.empty} <Link to="/admin/classes/new">{t.emptyLink}</Link>.
        </p>
      ) : (
        <div className="table-scroll">
          <table>
            <caption className="visually-hidden">{t.caption}</caption>
            <thead>
              <tr>
                <th scope="col">{t.columns.name}</th>
                <th scope="col" className="num">{t.columns.total}</th>
                <th scope="col" className="num">{t.columns.held}</th>
                <th scope="col" className="num">{t.columns.confirmed}</th>
                <th scope="col" className="num">{t.columns.available}</th>
                <th scope="col">
                  <span className="visually-hidden">{t.columns.action}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {classes.data.map((c) => (
                <tr key={c.id}>
                  <th scope="row">{c.name}</th>
                  <td className="num">{c.totalSeats}</td>
                  <td className="num">{c.heldSeats}</td>
                  <td className="num">{c.confirmedSeats}</td>
                  <td className="num">{c.availableSeats}</td>
                  <td>
                    <ApplyCell
                      cls={c}
                      hold={mine.get(c.id)}
                      hasStudent={studentId !== null}
                      busy={apply.isPending}
                      applyingThis={apply.isPending && apply.variables === c.id}
                      hintId={hintId}
                      onApply={onApply}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

interface ApplyCellProps {
  cls: ClassSummary;
  hold: MyHold | undefined;
  hasStudent: boolean;
  busy: boolean;
  applyingThis: boolean;
  hintId: string;
  onApply: (classId: string) => void;
}

/** Apply button rules (Design.md §4.1). */
function ApplyCell({ cls, hold, hasStudent, busy, applyingThis, hintId, onApply }: ApplyCellProps) {
  if (hold?.status === 'CONFIRMED') {
    return <Link className="button secondary" to={`/applications/${hold.holdId}`}>{t.admitted}</Link>;
  }
  if (hold) {
    return <Link className="button secondary" to={`/applications/${hold.holdId}`}>{t.viewApplication}</Link>;
  }
  if (!hasStudent) {
    return (
      <button type="button" disabled aria-describedby={hintId} title={t.needStudentId}>
        {t.apply}
      </button>
    );
  }
  if (cls.availableSeats === 0) {
    return <button type="button" disabled>{t.full}</button>;
  }
  return (
    <button type="button" disabled={busy} aria-label={applyingThis ? t.applying : t.applyFor(cls.name)} onClick={() => onApply(cls.id)}>
      {applyingThis ? t.applying : t.apply}
    </button>
  );
}

/** "Open it" / "View" for the 409s that point at an existing application. */
function existingHoldLink(error: unknown) {
  if (!(error instanceof ApiError) || !error.problem.existingHoldId) return undefined;
  const label = error.code === 'ALREADY_ADMITTED' ? messages.errors.view : messages.errors.openIt;
  return <Link to={`/applications/${error.problem.existingHoldId}`}>{label}</Link>;
}
