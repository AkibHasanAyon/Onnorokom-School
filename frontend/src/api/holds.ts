import { apiFetch } from './client';
import type { Health, HoldConfirmed, HoldCreated, HoldDetails, MyHold } from './types';

const holdPath = (holdId: string) => `/holds/${encodeURIComponent(holdId)}`;

/** Apply: POST /classes/{classId}/holds (409 NO_SEATS_AVAILABLE when full). */
export const createHold = (classId: string, studentId: string) =>
  apiFetch<HoldCreated>(`/classes/${encodeURIComponent(classId)}/holds`, { method: 'POST', studentId });

/** GET /holds/{holdId} — no header required. */
export const getHold = (holdId: string, signal?: AbortSignal) =>
  apiFetch<HoldDetails>(holdPath(holdId), { signal });

/** Pay (simulated): POST /holds/{holdId}/confirm. */
export const confirmHold = (holdId: string, studentId: string) =>
  apiFetch<HoldConfirmed>(`${holdPath(holdId)}/confirm`, { method: 'POST', studentId });

/** Cancel: DELETE /holds/{holdId} → 204. */
export const cancelHold = (holdId: string, studentId: string) =>
  apiFetch<void>(holdPath(holdId), { method: 'DELETE', studentId });

/** GET /me/holds — newest first, all statuses. */
export const listMyHolds = (studentId: string, signal?: AbortSignal) =>
  apiFetch<MyHold[]>('/me/holds', { studentId, signal });

/** GET /health — used once at start-up to learn the server clock offset. */
export const getHealth = () => apiFetch<Health>('/health', { timeoutMs: 5_000 });
