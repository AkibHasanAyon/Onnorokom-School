import type { HoldStatus } from '../api/types';

/** The literal status word (R9), coloured by state — the colour is never the only signal. */
export function StatusBadge({ status }: { status: HoldStatus }) {
  return <span className={`badge badge-${status.toLowerCase()}`}>{status}</span>;
}
