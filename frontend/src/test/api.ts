import { http, HttpResponse } from 'msw';
import type { ClassSummary, HoldDetails, MyHold } from '../api/types';
import { problem } from './handlers';
import { asMyHold } from './fixtures';
import { server } from './server';

/**
 * Per-test API setups on top of the default "empty backend" handlers.
 * Each returns what it recorded, so tests can assert on the requests the UI sent.
 */

/** GET /classes answers with these classes. */
export function givenClasses(...classes: ClassSummary[]) {
  server.use(http.get('*/classes', () => HttpResponse.json(classes)));
}

/** GET /me/holds answers with these holds, whatever the student id. */
export function givenMyHolds(...holds: MyHold[]) {
  server.use(http.get('*/me/holds', () => HttpResponse.json(holds)));
}

/** GET /me/holds answers per X-Student-Id (unknown ids get []). Returns the ids asked for, in order. */
export function givenMyHoldsByStudent(byStudent: Record<string, MyHold[]>) {
  const asked: (string | null)[] = [];
  server.use(
    http.get('*/me/holds', ({ request }) => {
      const id = request.headers.get('X-Student-Id');
      asked.push(id);
      return HttpResponse.json(byStudent[id ?? ''] ?? []);
    }),
  );
  return asked;
}

/**
 * GET /holds/{id} answers with `current()` (re-read on every request, so a test can change it), other ids
 * get 404. Unless `mine: false`, GET /me/holds lists it too, so the saved student owns it.
 */
export function givenHold(current: () => HoldDetails, { mine = true }: { mine?: boolean } = {}) {
  server.use(
    http.get('*/holds/:holdId', ({ params }) =>
      params.holdId === current().holdId ? HttpResponse.json(current()) : problem(404, 'HOLD_NOT_FOUND'),
    ),
    http.get('*/me/holds', () => HttpResponse.json(mine ? [asMyHold(current())] : [])),
  );
}

export interface RecordedRequest {
  method: string;
  path: string;
  studentId: string | null;
}

/** Records every request matching `method` + `path` pattern and answers with `respond()`. */
export function recordRequests(method: 'get' | 'post' | 'delete', path: string, respond: () => Response) {
  const requests: RecordedRequest[] = [];
  server.use(
    http[method](path, ({ request }) => {
      requests.push({ method: request.method, path: new URL(request.url).pathname, studentId: request.headers.get('X-Student-Id') });
      return respond();
    }),
  );
  return requests;
}
