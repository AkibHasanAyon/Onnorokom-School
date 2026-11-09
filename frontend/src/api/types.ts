/**
 * HTTP contract types — mirror docs/API.md exactly. Contract fields are required;
 * extra (non-contract) fields are optional so the UI degrades gracefully if they're absent.
 */

export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';

/** GET /classes item and GET /classes/{classId} (API.md §4.2, §4.3). */
export interface ClassSummary {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

/** POST /classes body (API.md §4.1). */
export interface CreateClassRequest {
  name: string;
  totalSeats: number;
}

/** POST /classes → 201 (API.md §4.1). */
export interface CreatedClass {
  id: string;
  name: string;
  totalSeats: number;
}

/** POST /classes/{classId}/holds → 201 (API.md §4.4). */
export interface HoldCreated {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
  className?: string;
  createdAt?: string;
  serverTime?: string;
}

/** GET /holds/{holdId} → 200 (API.md §4.5). `status` is the effective status. */
export interface HoldDetails {
  holdId: string;
  classId: string;
  status: HoldStatus;
  expiresAt: string;
  className?: string;
  createdAt?: string;
  confirmedAt?: string | null;
  cancelledAt?: string | null;
  serverTime?: string;
}

/** POST /holds/{holdId}/confirm → 200 (API.md §4.6). */
export interface HoldConfirmed {
  holdId: string;
  status: 'CONFIRMED';
  classId?: string;
  confirmedAt?: string;
}

/** GET /me/holds item (API.md §4.8). */
export interface MyHold {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
  createdAt?: string;
}

/** GET /health (API.md §4.9). */
export interface Health {
  status: 'ok' | 'unavailable';
  serverTime: string;
}

/** Error body: RFC 9457 problem+json with the stable `code` (API.md §3). */
export interface Problem {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  existingHoldId?: string;
}
