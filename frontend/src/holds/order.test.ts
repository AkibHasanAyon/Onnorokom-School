import { describe, expect, it } from 'vitest';
import type { HoldStatus, MyHold } from '../api/types';
import { orderApplications } from './order';

const NOW = Date.parse('2026-11-01T04:00:00Z');
const iso = (offsetSeconds: number) => new Date(NOW + offsetSeconds * 1000).toISOString();

function mine(id: string, status: HoldStatus, createdAgoSeconds: number, expiresInSeconds = 600): MyHold {
  return {
    holdId: id, classId: `c-${id}`, className: id, status,
    expiresAt: iso(expiresInSeconds), createdAt: iso(-createdAgoSeconds),
  };
}

const ids = (holds: MyHold[]) => orderApplications(holds, NOW).map((r) => r.hold.holdId);

describe('orderApplications', () => {
  it('puts active applications first, then the rest newest first', () => {
    const holds = [
      mine('confirmed-new', 'CONFIRMED', 10),
      mine('held-old', 'HELD', 300),
      mine('cancelled-mid', 'CANCELLED', 100),
      mine('held-new', 'HELD', 20),
      mine('expired-old', 'EXPIRED', 900, -300),
    ];

    expect(ids(holds)).toEqual(['held-new', 'held-old', 'confirmed-new', 'cancelled-mid', 'expired-old']);
  });

  it('treats a HELD application past its expiry as EXPIRED (not active)', () => {
    const rows = orderApplications([mine('confirmed', 'CONFIRMED', 50), mine('lapsed', 'HELD', 10, -1)], NOW);

    expect(rows.map((r) => [r.hold.holdId, r.status])).toEqual([
      ['lapsed', 'EXPIRED'],
      ['confirmed', 'CONFIRMED'],
    ]);
  });

  it("keeps the server's order when createdAt is missing", () => {
    const strip = ({ createdAt: _ignored, ...rest }: MyHold): MyHold => rest;
    const holds = [mine('a', 'CONFIRMED', 1), mine('b', 'CANCELLED', 2), mine('c', 'EXPIRED', 3, -5)].map(strip);

    expect(ids(holds)).toEqual(['a', 'b', 'c']);
  });
});
