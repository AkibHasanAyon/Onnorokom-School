import { describe, expect, it } from 'vitest';
import { DEFAULT_API_BASE_URL, resolveApiBaseUrl } from './config';

describe('resolveApiBaseUrl', () => {
  it('prefers the runtime value from /config.js', () => {
    expect(resolveApiBaseUrl('http://localhost:9090', 'http://build:1')).toBe('http://localhost:9090');
  });

  it('falls back to the build-time value, then the default', () => {
    expect(resolveApiBaseUrl(undefined, 'http://build:1')).toBe('http://build:1');
    expect(resolveApiBaseUrl('', '  ')).toBe(DEFAULT_API_BASE_URL);
    expect(resolveApiBaseUrl(undefined, undefined)).toBe('http://localhost:8080');
  });

  it('strips trailing slashes', () => {
    expect(resolveApiBaseUrl('http://localhost:8080///', undefined)).toBe('http://localhost:8080');
  });
});
