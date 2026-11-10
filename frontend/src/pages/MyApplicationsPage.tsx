import { Link } from 'react-router';
import { serverNow } from '../api/serverClock';
import { ErrorAlert } from '../components/ErrorAlert';
import { StatusBadge } from '../components/StatusBadge';
import { formatCountdown, secondsLeft } from '../holds/countdown';
import { orderApplications } from '../holds/order';
import { useMyHolds } from '../hooks/queries';
import { useNow } from '../hooks/useNow';
import { messages } from '../i18n/messages';
import { useStudent } from '../student/useStudent';

const t = messages.myApplications;

/** My Applications (Design.md §4.4): every application for the saved number, with live status (R11). */
export function MyApplicationsPage() {
  const { studentId } = useStudent();
  const myHolds = useMyHolds(studentId);
  useNow(250); // live countdowns; a HELD row flips to EXPIRED at 0 (ASSUMPTIONS #28)

  if (!studentId) {
    return (
      <>
        <h1>{messages.pages.myApplications}</h1>
        <p className="notice">{t.needStudentId}</p>
      </>
    );
  }

  const nowMs = serverNow();

  return (
    <>
      <h1>{t.titleFor(studentId)}</h1>
      <p className="muted">{t.autoRefresh}</p>

      {myHolds.isPending ? (
        <p role="status">{t.loading}</p>
      ) : myHolds.isError && !myHolds.data ? (
        <ErrorAlert error={myHolds.error} onRetry={() => void myHolds.refetch()} />
      ) : myHolds.data.length === 0 ? (
        <p>
          {t.empty} <Link to="/">{t.emptyLink}</Link> {t.emptyAfter}
        </p>
      ) : (
        <div className="table-scroll">
          <table>
            <caption className="visually-hidden">{t.caption}</caption>
            <thead>
              <tr>
                <th scope="col">{t.columns.name}</th>
                <th scope="col">{t.columns.status}</th>
                <th scope="col" className="num">{t.columns.timeLeft}</th>
                <th scope="col">
                  <span className="visually-hidden">{t.columns.action}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {orderApplications(myHolds.data, nowMs).map(({ hold, status }) => (
                <tr key={hold.holdId}>
                  <th scope="row">{hold.className}</th>
                  <td>
                    <StatusBadge status={status} />
                  </td>
                  <td className="num">
                    {status === 'HELD' ? (
                      <time dateTime={hold.expiresAt} title={hold.expiresAt}>
                        {formatCountdown(secondsLeft(hold.expiresAt, nowMs))}
                      </time>
                    ) : (
                      t.none
                    )}
                  </td>
                  <td>
                    <Link className="button secondary" to={`/applications/${hold.holdId}`} aria-label={t.openFor(hold.className)}>
                      {t.open}
                    </Link>
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
