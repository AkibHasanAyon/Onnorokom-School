import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useId, useState } from 'react';
import { Link, useParams } from 'react-router';
import { serverNow } from '../api/serverClock';
import type { HoldDetails, HoldStatus } from '../api/types';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorAlert } from '../components/ErrorAlert';
import { FreshnessNote } from '../components/FreshnessNote';
import { StatusBadge } from '../components/StatusBadge';
import { effectiveStatus, formatClockTime, formatCountdown, secondsLeft } from '../holds/countdown';
import { queryKeys, useCancelHold, useConfirmHold, useHold, useMyHolds } from '../hooks/queries';
import { useNow } from '../hooks/useNow';
import { messages } from '../i18n/messages';
import { useStudent } from '../student/useStudent';

const t = messages.application;
const URGENT_SECONDS = 60;

/** Whether the saved number may pay/cancel. GET /holds has no owner field, so "My applications" decides. */
type Access = 'owner' | 'other' | 'no-student';

/** Application (Design.md §4.3): live status and countdown (R9), Pay and Cancel. */
export function ApplicationPage() {
  const { holdId = '' } = useParams();
  const { studentId } = useStudent();
  const hold = useHold(holdId);
  const myHolds = useMyHolds(studentId);

  const access: Access = !studentId
    ? 'no-student'
    : myHolds.data && !myHolds.data.some((h) => h.holdId === holdId)
      ? 'other'
      : 'owner'; // while "My applications" loads, the server still enforces ownership (403 HOLD_NOT_OWNED)

  if (hold.isPending) {
    return (
      <>
        <h1>{messages.pages.application}</h1>
        <p role="status">{t.loading}</p>
      </>
    );
  }

  if (hold.isError && !hold.data) {
    return (
      <>
        <h1>{messages.pages.application}</h1>
        <ErrorAlert
          error={hold.error}
          onRetry={() => void hold.refetch()}
          action={<Link to="/">{t.backToClasses}</Link>}
        />
      </>
    );
  }

  return (
    <HoldView
      hold={hold.data}
      access={access}
      studentId={studentId}
      updatedAt={hold.dataUpdatedAt}
      stale={hold.isError}
    />
  );
}

interface HoldViewProps {
  hold: HoldDetails;
  access: Access;
  studentId: string | null;
  /** When the hold was last fetched, and whether the latest refresh failed (stale-data note). */
  updatedAt: number;
  stale: boolean;
}

function HoldView({ hold, access, studentId, updatedAt, stale }: HoldViewProps) {
  const queryClient = useQueryClient();
  const confirm = useConfirmHold(hold.holdId, studentId);
  const cancel = useCancelHold(hold.holdId, studentId);
  const [askCancel, setAskCancel] = useState(false);
  const payReasonId = useId();
  useNow(250); // ASSUMPTIONS #28: tick every 250 ms so EXPIRED shows the moment the countdown reaches 0

  const nowMs = serverNow();
  const status: HoldStatus = effectiveStatus(hold.status, hold.expiresAt, nowMs);
  const left = secondsLeft(hold.expiresAt, nowMs);
  const expiredLocally = hold.status === 'HELD' && status === 'EXPIRED';

  // The countdown hit 0: the page already shows EXPIRED; ask the server so stored state and seat counts follow.
  useEffect(() => {
    if (!expiredLocally) return;
    void queryClient.invalidateQueries({ queryKey: queryKeys.hold(hold.holdId) });
    void queryClient.invalidateQueries({ queryKey: queryKeys.classes });
  }, [expiredLocally, hold.holdId, queryClient]);

  const canAct = access === 'owner';
  const busy = confirm.isPending || cancel.isPending;
  const mutationError = confirm.error ?? cancel.error;

  return (
    <>
      <h1>{hold.className ? t.titleFor(hold.className) : messages.pages.application}</h1>
      {(stale || status === 'HELD') && (
        <p className="muted">
          <FreshnessNote updatedAt={updatedAt} stale={stale} />
        </p>
      )}

      <dl className="hold-facts">
        <div>
          <dt>{t.statusLabel}</dt>
          <dd>
            <StatusBadge status={status} /> <span>{t.status[status]}</span>
          </dd>
        </div>
        {status === 'HELD' && (
          <div>
            <dt>{t.timeLeftLabel}</dt>
            <dd>
              <span className={left <= URGENT_SECONDS ? 'countdown countdown-urgent' : 'countdown'} data-testid="countdown">
                {formatCountdown(left)}
              </span>
              {left <= URGENT_SECONDS && <strong className="countdown-urgent"> {t.hurry}</strong>}{' '}
              <time className="muted" dateTime={hold.expiresAt} title={hold.expiresAt}>
                {t.expiresAt(formatClockTime(hold.expiresAt))}
              </time>
            </dd>
          </div>
        )}
      </dl>

      {/* Announced only when its text changes: at one minute left and at expiry — not every tick. */}
      <p className="visually-hidden" aria-live="polite">
        {status === 'HELD' && left <= URGENT_SECONDS ? t.announceOneMinute : status === 'EXPIRED' ? t.announceExpired : ''}
      </p>

      {mutationError && <ErrorAlert error={mutationError} />}
      {confirm.isSuccess && status === 'CONFIRMED' && (
        <p className="alert alert-success" role="status">
          {t.paid}
        </p>
      )}
      {cancel.isSuccess && status === 'CANCELLED' && (
        <p className="alert alert-success" role="status">
          {t.cancelled}
        </p>
      )}

      {access === 'other' && <p className="notice">{t.notOwned}</p>}
      {access === 'no-student' && (status === 'HELD' || status === 'EXPIRED') && (
        <p className="notice">{t.needStudentId}</p>
      )}

      {canAct && (status === 'HELD' || status === 'EXPIRED') && (
        <>
          <div className="actions">
            <button
              type="button"
              disabled={status !== 'HELD' || busy}
              aria-describedby={status === 'EXPIRED' ? payReasonId : undefined}
              onClick={() => {
                cancel.reset();
                confirm.mutate();
              }}
            >
              {confirm.isPending ? t.paying : t.pay}
            </button>
            {status === 'HELD' && (
              <button type="button" className="secondary" disabled={busy} onClick={() => setAskCancel(true)}>
                {cancel.isPending ? t.cancelling : t.cancel}
              </button>
            )}
          </div>
          {status === 'HELD' ? (
            <p className="muted">{t.simulated}</p>
          ) : (
            <p id={payReasonId} className="muted">
              {t.payDisabledExpired}
            </p>
          )}
        </>
      )}

      {(status === 'EXPIRED' || status === 'CANCELLED') && (
        <p>
          <Link to="/">{t.backToClasses}</Link>
        </p>
      )}
      {status === 'CONFIRMED' && (
        <p>
          <Link to="/my-applications">{t.myApplications}</Link>
        </p>
      )}

      {askCancel && status === 'HELD' && (
        <ConfirmDialog
          title={t.confirmTitle}
          body={t.confirmBody}
          confirmLabel={t.confirmYes}
          cancelLabel={t.confirmNo}
          onClose={() => setAskCancel(false)}
          onConfirm={() => {
            setAskCancel(false);
            confirm.reset();
            cancel.mutate();
          }}
        />
      )}
    </>
  );
}
