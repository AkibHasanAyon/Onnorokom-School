import type { ClassSummary, HoldDetails, MyHold } from '../api/types';

/** Test data shared by all frontend tests (shapes mirror docs/API.md). */

export const STUDENT = '20152691234567890';
export const OTHER_STUDENT = '20152691234500000';
export const HOLD_ID = '0193a6f3-0a11-7c22-8e33-445566778899';

/** ISO time relative to now (respects fake timers, since it reads Date.now()). */
export const inSeconds = (s: number) => new Date(Date.now() + s * 1000).toISOString();
export const inMinutes = (m: number) => inSeconds(m * 60);

export const morningShift: ClassSummary = {
  id: 'c-morning', name: 'Class 6 - Morning Shift', totalSeats: 120, heldSeats: 15, confirmedSeats: 80, availableSeats: 25,
};

/** A full class: held + confirmed = total. */
export const dayShift: ClassSummary = {
  id: 'c-day', name: 'Class 6 - Day Shift', totalSeats: 120, heldSeats: 2, confirmedSeats: 118, availableSeats: 0,
};

export function aClass(overrides: Partial<ClassSummary> = {}): ClassSummary {
  return { ...morningShift, ...overrides };
}

/** GET /holds/{id} body: a HELD hold for the morning shift with 10 minutes left, unless overridden. */
export function aHold(overrides: Partial<HoldDetails> = {}): HoldDetails {
  return {
    holdId: HOLD_ID,
    classId: morningShift.id,
    className: morningShift.name,
    status: 'HELD',
    expiresAt: inSeconds(600),
    ...overrides,
  };
}

/** GET /me/holds item. */
export function aMyHold(overrides: Partial<MyHold> = {}): MyHold {
  return {
    holdId: HOLD_ID,
    classId: morningShift.id,
    className: morningShift.name,
    status: 'HELD',
    expiresAt: inSeconds(600),
    ...overrides,
  };
}

/** The same hold as it appears in GET /me/holds. */
export function asMyHold(hold: HoldDetails): MyHold {
  return {
    holdId: hold.holdId,
    classId: hold.classId,
    className: hold.className ?? '',
    status: hold.status,
    expiresAt: hold.expiresAt,
    ...(hold.createdAt ? { createdAt: hold.createdAt } : {}),
  };
}
