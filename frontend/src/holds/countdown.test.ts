import { describe, expect, it } from 'vitest';
import { effectiveStatus, formatCountdown, secondsLeft } from './countdown';

const NOW = Date.parse('2026-11-01T04:00:00Z');
const at = (offsetSeconds: number) => new Date(NOW + offsetSeconds * 1000).toISOString();

describe('effectiveStatus', () => {
  it('keeps HELD while time is left', () => {
    expect(effectiveStatus('HELD', at(1), NOW)).toBe('HELD');
  });

  it('turns HELD into EXPIRED exactly at expiresAt and after it (R9)', () => {
    expect(effectiveStatus('HELD', at(0), NOW)).toBe('EXPIRED');
    expect(effectiveStatus('HELD', at(-30), NOW)).toBe('EXPIRED');
  });

  it.each(['CONFIRMED', 'CANCELLED', 'EXPIRED'] as const)('never changes %s', (status) => {
    expect(effectiveStatus(status, at(-30), NOW)).toBe(status);
    expect(effectiveStatus(status, at(30), NOW)).toBe(status);
  });

  it('keeps the server status when expiresAt is unreadable', () => {
    expect(effectiveStatus('HELD', 'not a date', NOW)).toBe('HELD');
  });
});

describe('secondsLeft', () => {
  it('rounds up partial seconds and never goes below zero', () => {
    expect(secondsLeft(at(600), NOW)).toBe(600);
    expect(secondsLeft(new Date(NOW + 1_200).toISOString(), NOW)).toBe(2);
    expect(secondsLeft(at(-5), NOW)).toBe(0);
    expect(secondsLeft('garbage', NOW)).toBe(0);
  });
});

describe('formatCountdown', () => {
  it.each([
    [0, '00:00'],
    [9, '00:09'],
    [462, '07:42'],
    [600, '10:00'],
    [3599, '59:59'],
    [3600, '1:00:00'],
    [3725, '1:02:05'],
    [-3, '00:00'],
  ])('%i s → %s', (seconds, text) => {
    expect(formatCountdown(seconds)).toBe(text);
  });
});
