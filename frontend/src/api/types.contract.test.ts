import { describe, expect, it } from 'vitest';
import type { ClassSummary, CreatedClass, HoldConfirmed, HoldCreated, HoldDetails, MyHold, Problem } from './types';

/**
 * The example payloads from docs/API.md, checked against the types with `satisfies`:
 * if a contract field is renamed or retyped, `npm run typecheck` fails here.
 */
const examples = {
  created: { id: '0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e', name: 'Class 6 - Morning Shift', totalSeats: 120 } satisfies CreatedClass,
  summary: {
    id: '0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e', name: 'Class 6 - Morning Shift',
    totalSeats: 120, heldSeats: 15, confirmedSeats: 80, availableSeats: 25,
  } satisfies ClassSummary,
  holdCreated: {
    holdId: '0193a6f3-0a11-7c22-8e33-445566778899', classId: '0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e',
    studentId: '20152691234567890', status: 'HELD', expiresAt: '2026-11-01T04:10:00Z',
    className: 'Class 6 - Morning Shift', createdAt: '2026-11-01T04:00:00Z', serverTime: '2026-11-01T04:00:00Z',
  } satisfies HoldCreated,
  hold: {
    holdId: '0193a6f3-0a11-7c22-8e33-445566778899', classId: '0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e',
    status: 'HELD', expiresAt: '2026-11-01T04:10:00Z', className: 'Class 6 - Morning Shift',
    createdAt: '2026-11-01T04:00:00Z', confirmedAt: null, cancelledAt: null, serverTime: '2026-11-01T04:03:12Z',
  } satisfies HoldDetails,
  confirmed: {
    holdId: '0193a6f3-0a11-7c22-8e33-445566778899', status: 'CONFIRMED',
    classId: '0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e', confirmedAt: '2026-11-01T04:05:41Z',
  } satisfies HoldConfirmed,
  myHold: {
    holdId: '0193a6f3-0a11-7c22-8e33-445566778899', classId: '0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e',
    className: 'Class 6 - Morning Shift', status: 'HELD', expiresAt: '2026-11-01T04:10:00Z', createdAt: '2026-11-01T04:00:00Z',
  } satisfies MyHold,
  problem: {
    type: 'https://httpstatuses.io/409', title: 'No seats available', status: 409,
    detail: "All seats in 'Class 6 - Morning Shift' are currently held or confirmed.",
    code: 'NO_SEATS_AVAILABLE', traceId: '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00',
  } satisfies Problem,
};

describe('contract types', () => {
  it('accept the API.md example payloads', () => {
    expect(Object.keys(examples.summary)).toEqual(['id', 'name', 'totalSeats', 'heldSeats', 'confirmedSeats', 'availableSeats']);
    expect(examples.holdCreated.status).toBe('HELD');
    expect(examples.confirmed.status).toBe('CONFIRMED');
  });
});
