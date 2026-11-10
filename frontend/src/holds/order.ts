import type { HoldStatus, MyHold } from '../api/types';
import { effectiveStatus } from './countdown';

export interface ApplicationRow {
  hold: MyHold;
  /** Effective status right now (a HELD hold past `expiresAt` is EXPIRED). */
  status: HoldStatus;
}

/**
 * My Applications order (Design.md §4.4): active (HELD, not yet expired) first, then newest first.
 * The server already sends newest first; `createdAt` is used when present and the sort is stable otherwise.
 */
export function orderApplications(holds: readonly MyHold[], serverNowMs: number): ApplicationRow[] {
  const created = (h: MyHold) => (h.createdAt ? Date.parse(h.createdAt) : Number.NaN);
  return holds
    .map((hold) => ({ hold, status: effectiveStatus(hold.status, hold.expiresAt, serverNowMs) }))
    .sort((a, b) => {
      const active = Number(b.status === 'HELD') - Number(a.status === 'HELD');
      if (active !== 0) return active;
      const byNewest = created(b.hold) - created(a.hold);
      return Number.isNaN(byNewest) ? 0 : byNewest;
    });
}
