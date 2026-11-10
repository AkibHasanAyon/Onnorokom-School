import type { HoldStatus } from '../api/types';

/**
 * The status to show right now. A HELD hold whose `expiresAt` has passed (on the server's clock) is
 * shown as EXPIRED at once, without waiting for the next poll (R9) — the server computes it the same way.
 */
export function effectiveStatus(status: HoldStatus, expiresAt: string, serverNowMs: number): HoldStatus {
  if (status !== 'HELD') return status;
  const expires = Date.parse(expiresAt);
  return Number.isNaN(expires) || expires > serverNowMs ? 'HELD' : 'EXPIRED';
}

/** Whole seconds left until `expiresAt` (never negative). Rounded up so 00:00 means "expired". */
export function secondsLeft(expiresAt: string, serverNowMs: number): number {
  const expires = Date.parse(expiresAt);
  if (Number.isNaN(expires)) return 0;
  return Math.max(0, Math.ceil((expires - serverNowMs) / 1000));
}

/** `mm:ss`, or `h:mm:ss` from one hour (Design.md §4.3). */
export function formatCountdown(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(s / 3600);
  const minutes = Math.floor((s % 3600) / 60);
  const seconds = s % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${pad(minutes)}:${pad(seconds)}`;
}

/** Local wall-clock time, e.g. "10:10:00 AM" (shown in the browser's time zone). */
export function formatClockTime(iso: string): string {
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? '' : new Date(ms).toLocaleTimeString();
}
