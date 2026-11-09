import { http, HttpResponse } from 'msw';

/**
 * Default API answers: an empty, healthy backend. Tests override the routes they care about.
 * Paths use `*` so they match whatever API_BASE_URL is configured.
 */
export const defaultHandlers = [
  http.get('*/health', () => HttpResponse.json({ status: 'ok', serverTime: new Date().toISOString().replace(/\.\d{3}Z$/, 'Z') })),
  http.get('*/classes', () => HttpResponse.json([])),
  http.get('*/me/holds', () => HttpResponse.json([])),
];

/** Problem+json error body like the backend sends (API.md §3). */
export function problem(status: number, code: string, extra: Record<string, unknown> = {}) {
  return HttpResponse.json(
    { type: `https://httpstatuses.io/${status}`, title: code, status, code, traceId: 'test', ...extra },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}
