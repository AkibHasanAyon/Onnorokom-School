import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { API_BASE_URL } from '../config';
import { ApiError, apiFetch } from './client';
import { cancelHold, confirmHold, createHold, getHold, listMyHolds } from './holds';
import { createClass, getClass, listClasses } from './classes';
import { getServerOffsetMs, resetServerOffset } from './serverClock';

const fetchMock = vi.fn<typeof fetch>();

const json = (status: number, body: unknown, contentType = 'application/json') =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } });

/** The (url, init) of the n-th fetch call. */
function call(n = 0): { url: string; init: RequestInit; headers: Record<string, string> } {
  const args = fetchMock.mock.calls[n];
  if (!args) throw new Error(`fetch call #${n} not made`);
  const init = args[1] ?? {};
  return { url: String(args[0]), init, headers: (init.headers ?? {}) as Record<string, string> };
}

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
  resetServerOffset();
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('apiFetch', () => {
  it('GETs JSON from the configured base URL without a body or content type', async () => {
    fetchMock.mockResolvedValue(json(200, [{ id: 'c1' }]));

    const result = await apiFetch<{ id: string }[]>('/classes');

    expect(result).toEqual([{ id: 'c1' }]);
    const { url, init, headers } = call();
    expect(url).toBe(`${API_BASE_URL}/classes`);
    expect(init.method).toBe('GET');
    expect(init.body).toBeUndefined();
    expect(headers.Accept).toBe('application/json');
    expect(headers['Content-Type']).toBeUndefined();
    expect(headers['X-Student-Id']).toBeUndefined();
  });

  it('sends JSON bodies with Content-Type and the student id header', async () => {
    fetchMock.mockResolvedValue(json(201, { id: 'c1' }));

    await apiFetch('/classes', { method: 'POST', body: { name: 'A', totalSeats: 3 }, studentId: '20152691234567890' });

    const { init, headers } = call();
    expect(init.method).toBe('POST');
    expect(init.body).toBe('{"name":"A","totalSeats":3}');
    expect(headers['Content-Type']).toBe('application/json');
    expect(headers['X-Student-Id']).toBe('20152691234567890');
  });

  it('returns undefined for 204 No Content', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));
    await expect(apiFetch('/holds/h1', { method: 'DELETE' })).resolves.toBeUndefined();
  });

  it('turns problem+json errors into ApiError with the server code and body', async () => {
    fetchMock.mockResolvedValue(
      json(409, { status: 409, code: 'ACTIVE_HOLD_EXISTS', detail: 'Already applied.', existingHoldId: 'h9' }, 'application/problem+json'),
    );

    const error = await apiFetch('/classes/c1/holds', { method: 'POST' }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.kind).toBe('http');
    expect(apiError.status).toBe(409);
    expect(apiError.code).toBe('ACTIVE_HOLD_EXISTS');
    expect(apiError.message).toBe('Already applied.');
    expect(apiError.problem.existingHoldId).toBe('h9');
  });

  it('uses HTTP_<status> when an error body is not JSON (e.g. a proxy page)', async () => {
    fetchMock.mockResolvedValue(new Response('<html>Bad Gateway</html>', { status: 502, statusText: 'Bad Gateway' }));

    const error = (await apiFetch('/classes').catch((e: unknown) => e)) as ApiError;

    expect(error.kind).toBe('http');
    expect(error.code).toBe('HTTP_502');
  });

  it('reports a network error when the server is unreachable', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    const error = (await apiFetch('/classes').catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.kind).toBe('network');
    expect(error.status).toBeNull();
    expect(error.code).toBe('NETWORK_ERROR');
  });

  it('reports a timeout when the server does not answer in time', async () => {
    vi.useFakeTimers();
    fetchMock.mockImplementation(
      (_url, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
        }),
    );

    const pending = apiFetch('/classes', { timeoutMs: 1_000 }).catch((e: unknown) => e);
    await vi.advanceTimersByTimeAsync(1_000);
    const error = (await pending) as ApiError;

    expect(error.kind).toBe('timeout');
    expect(error.code).toBe('TIMEOUT');
  });

  it('lets a caller cancellation through as an AbortError, not an ApiError', async () => {
    fetchMock.mockImplementation(
      (_url, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
        }),
    );
    const caller = new AbortController();

    const pending = apiFetch('/classes', { signal: caller.signal }).catch((e: unknown) => e);
    caller.abort();
    const error = await pending;

    expect(error).not.toBeInstanceOf(ApiError);
    expect((error as DOMException).name).toBe('AbortError');
  });

  it('learns the server clock offset from serverTime', async () => {
    const serverTime = new Date(Date.now() + 120_000);
    serverTime.setMilliseconds(0);
    fetchMock.mockResolvedValue(json(200, { holdId: 'h1', serverTime: serverTime.toISOString().replace('.000', '') }));

    await apiFetch('/holds/h1');

    expect(getServerOffsetMs()).toBeGreaterThan(118_000);
    expect(getServerOffsetMs()).toBeLessThan(121_000);
  });
});

describe('API functions', () => {
  beforeEach(() => fetchMock.mockImplementation(async () => json(200, {})));

  it.each([
    ['listClasses', () => listClasses(), 'GET', '/classes', undefined],
    ['getClass', () => getClass('c 1'), 'GET', '/classes/c%201', undefined],
    ['createClass', () => createClass({ name: 'A', totalSeats: 2 }), 'POST', '/classes', undefined],
    ['createHold', () => createHold('c1', '20152691234567890'), 'POST', '/classes/c1/holds', '20152691234567890'],
    ['getHold', () => getHold('h/1'), 'GET', '/holds/h%2F1', undefined],
    ['confirmHold', () => confirmHold('h1', '20152691234567890'), 'POST', '/holds/h1/confirm', '20152691234567890'],
    ['cancelHold', () => cancelHold('h1', '20152691234567890'), 'DELETE', '/holds/h1', '20152691234567890'],
    ['listMyHolds', () => listMyHolds('20152691234567890'), 'GET', '/me/holds', '20152691234567890'],
  ] as const)('%s calls %s %s', async (_name, run, method, path, studentId) => {
    await run();

    const { url, init, headers } = call();
    expect(init.method).toBe(method);
    expect(url).toBe(`${API_BASE_URL}${path}`);
    expect(headers['X-Student-Id']).toBe(studentId);
  });
});
