import { useConnectionProblem } from '../hooks/useConnectionProblem';
import { useNow } from '../hooks/useNow';
import { messages } from '../i18n/messages';

/**
 * Shown under the header only while refreshes are failing (Design.md §2); disappears by itself once a
 * refresh succeeds. role="status", not "alert": the page keeps working with the last data.
 */
export function ConnectionBanner() {
  const problem = useConnectionProblem();
  if (!problem) return null;
  return <BannerText kind={problem.kind} lastSuccessAt={problem.lastSuccessAt} />;
}

function BannerText({ kind, lastSuccessAt }: { kind: 'network' | 'server'; lastSuccessAt: number | null }) {
  const now = useNow(1_000);
  const t = messages.connection;
  const age = lastSuccessAt === null ? null : Math.max(0, Math.floor((now - lastSuccessAt) / 1000));
  return (
    <div className="connection-banner" role="status">
      <div className="container">
        <span aria-hidden="true">⚠ </span>
        {kind === 'network' ? t.unreachable : t.serverTrouble}
        {age !== null && ` ${t.showingDataFrom(age)}`} {t.retrying}
      </div>
    </div>
  );
}
