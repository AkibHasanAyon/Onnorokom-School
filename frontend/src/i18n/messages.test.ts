import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import { errorMessage, messages } from './messages';

const http = (status: number, code: string) => new ApiError('http', status, code, '');

afterEach(() => vi.restoreAllMocks());

describe('errorMessage', () => {
  it.each([
    'NO_SEATS_AVAILABLE', 'ACTIVE_HOLD_EXISTS', 'ALREADY_ADMITTED', 'HOLD_EXPIRED', 'HOLD_CANCELLED',
    'HOLD_ALREADY_CONFIRMED', 'HOLD_NOT_OWNED', 'INVALID_STUDENT_ID', 'CLASS_NAME_EXISTS', 'VALIDATION_FAILED',
    'MALFORMED_JSON', 'CLASS_NOT_FOUND', 'HOLD_NOT_FOUND', 'NOT_FOUND', 'SERVICE_UNAVAILABLE',
  ] as const)('maps %s to its Design.md sentence', (code) => {
    expect(errorMessage(http(409, code))).toBe(messages.errors[code]);
  });

  it('never shows raw codes', () => {
    const text = errorMessage(http(409, 'NO_SEATS_AVAILABLE'));
    expect(text).not.toMatch(/NO_SEATS|409/);
  });

  it('explains network failures and timeouts', () => {
    expect(errorMessage(new ApiError('network', null, 'NETWORK_ERROR', ''))).toMatch(/Can't reach the server/);
    expect(errorMessage(new ApiError('timeout', null, 'TIMEOUT', ''))).toMatch(/taking too long/);
  });

  it('uses the server-trouble sentence for unknown 5xx codes', () => {
    expect(errorMessage(http(502, 'HTTP_502'))).toBe(messages.errors.server);
  });

  it('falls back to a generic sentence and logs unexpected errors for developers', () => {
    const log = vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(errorMessage(new TypeError('x is undefined'))).toBe('Something went wrong. Please try again.');
    expect(errorMessage(http(418, 'TEAPOT'))).toBe('Something went wrong. Please try again.');
    expect(log).toHaveBeenCalledTimes(2);
  });
});
