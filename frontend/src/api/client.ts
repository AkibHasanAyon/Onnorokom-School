import { API_BASE_URL } from '../config';
import { reportServerTime } from './serverClock';
import type { Problem } from './types';

export type ApiErrorKind = 'http' | 'network' | 'timeout';

/**
 * Every failed API call becomes an ApiError: an HTTP error with the server's stable `code`,
 * a network failure (server unreachable), or a timeout. The UI maps these to friendly text.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  /** HTTP status, or null for network/timeout failures. */
  readonly status: number | null;
  /** Server `code` (e.g. NO_SEATS_AVAILABLE), `HTTP_<status>` if the body had none, or NETWORK_ERROR / TIMEOUT. */
  readonly code: string;
  /** The parsed problem+json body (e.g. `errors`, `existingHoldId`). */
  readonly problem: Problem;

  constructor(kind: ApiErrorKind, status: number | null, code: string, message: string, problem: Problem = {}) {
    super(message);
    this.name = 'ApiError';
    this.kind = kind;
    this.status = status;
    this.code = code;
    this.problem = problem;
  }
}

export const DEFAULT_TIMEOUT_MS = 10_000;

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'DELETE';
  body?: unknown;
  /** Sent as X-Student-Id. Passed explicitly by callers (from StudentContext), never read from storage here. */
  studentId?: string;
  timeoutMs?: number;
  /** Cancellation from the caller (e.g. TanStack Query). Rethrown as an AbortError, not an ApiError. */
  signal?: AbortSignal;
}

/** The only place the frontend calls fetch. */
export async function apiFetch<T>(path: string, opts: RequestOptions = {}): Promise<T> {
  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, opts.timeoutMs ?? DEFAULT_TIMEOUT_MS);
  const onCallerAbort = () => controller.abort();
  opts.signal?.addEventListener('abort', onCallerAbort);

  const headers: Record<string, string> = { Accept: 'application/json' };
  if (opts.body !== undefined) headers['Content-Type'] = 'application/json';
  if (opts.studentId) headers['X-Student-Id'] = opts.studentId;

  let res: Response;
  try {
    res = await fetch(`${API_BASE_URL}${path}`, {
      method: opts.method ?? 'GET',
      headers,
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
      signal: controller.signal,
    });
  } catch (error) {
    if (opts.signal?.aborted && !timedOut) throw error; // cancelled by the caller
    throw timedOut
      ? new ApiError('timeout', null, 'TIMEOUT', 'The server took too long to respond.')
      : new ApiError('network', null, 'NETWORK_ERROR', "Can't reach the server.");
  } finally {
    clearTimeout(timer);
    opts.signal?.removeEventListener('abort', onCallerAbort);
  }

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  let data: unknown;
  try {
    data = text ? JSON.parse(text) : undefined;
  } catch {
    data = undefined; // e.g. an HTML error page from a proxy
  }
  const obj = (data !== null && typeof data === 'object' && !Array.isArray(data) ? data : {}) as Record<string, unknown>;

  if (!res.ok) {
    const code = typeof obj.code === 'string' ? obj.code : `HTTP_${res.status}`;
    const detail = typeof obj.detail === 'string' ? obj.detail : res.statusText;
    throw new ApiError('http', res.status, code, detail, obj as Problem);
  }

  if (typeof obj.serverTime === 'string') reportServerTime(obj.serverTime);
  return data as T;
}
