import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { apiFetch, ApiError } from '../api/client';
import { getServerOffsetMs, reportServerTime } from '../api/serverClock';
import type { ClassSummary, Health, MyHold } from '../api/types';
import { queryKeys } from '../hooks/queries';
import { givenClasses, givenHold, recordRequests } from './api';
import { aHold, HOLD_ID, morningShift, STUDENT } from './fixtures';
import { renderApp } from './render';

/**
 * The test harness itself (Testing_QA.md §5 setup): every test starts from a clean, predictable state,
 * and the fake backend fails loudly instead of silently.
 */
describe('test harness', () => {
  describe('isolation between tests (these run in order)', () => {
    it('step 1: leaves state behind on purpose', async () => {
      localStorage.setItem('sas.studentId', STUDENT);
      vi.useFakeTimers();
      reportServerTime(new Date(Date.now() + 60_000).toISOString());
      givenClasses(morningShift);
      expect(await apiFetch<ClassSummary[]>('/classes')).toHaveLength(1);
    });

    it('step 2: storage, timers, clock offset and handlers are back to defaults', async () => {
      expect(localStorage.getItem('sas.studentId')).toBeNull();
      expect(vi.isFakeTimers()).toBe(false);
      expect(getServerOffsetMs()).toBe(0);
      expect(await apiFetch<ClassSummary[]>('/classes')).toEqual([]);
    });
  });

  it('gives every renderApp call its own empty query cache', () => {
    const first = renderApp('/');
    first.queryClient.setQueryData(queryKeys.classes, [morningShift]);
    first.unmount();

    const second = renderApp('/');

    expect(second.queryClient.getQueryData(queryKeys.classes)).toBeUndefined();
  });

  it('renderApp can start with a saved student id', async () => {
    renderApp('/', { studentId: STUDENT });

    expect(await screen.findByDisplayValue(STUDENT)).toBeInTheDocument();
  });

  it('the default backend is healthy and empty', async () => {
    await expect(apiFetch<Health>('/health')).resolves.toMatchObject({ status: 'ok' });
    await expect(apiFetch<ClassSummary[]>('/classes')).resolves.toEqual([]);
    await expect(apiFetch<MyHold[]>('/me/holds', { studentId: STUDENT })).resolves.toEqual([]);
    await expect(apiFetch(`/holds/${HOLD_ID}`)).rejects.toMatchObject({ status: 404, code: 'HOLD_NOT_FOUND' });
  });

  it('fails requests that no handler expects, instead of reaching a real server', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});

    const error = await apiFetch('/not-in-the-api').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ kind: 'network' });
    consoleError.mockRestore();
  });

  it('givenHold re-reads the hold on every request and 404s other ids', async () => {
    let current = aHold({ status: 'HELD' });
    givenHold(() => current);

    await expect(apiFetch(`/holds/${HOLD_ID}`)).resolves.toMatchObject({ status: 'HELD' });
    current = { ...current, status: 'CONFIRMED' };
    await expect(apiFetch(`/holds/${HOLD_ID}`)).resolves.toMatchObject({ status: 'CONFIRMED' });
    await expect(apiFetch('/holds/someone-else')).rejects.toMatchObject({ code: 'HOLD_NOT_FOUND' });
    await expect(apiFetch<MyHold[]>('/me/holds', { studentId: STUDENT })).resolves.toHaveLength(1);
  });

  it('recordRequests captures method, path and X-Student-Id', async () => {
    const requests = recordRequests('post', '*/holds/:holdId/confirm', () => Response.json({ holdId: HOLD_ID, status: 'CONFIRMED' }));

    await apiFetch(`/holds/${HOLD_ID}/confirm`, { method: 'POST', studentId: STUDENT });

    expect(requests).toEqual([{ method: 'POST', path: `/holds/${HOLD_ID}/confirm`, studentId: STUDENT }]);
  });
});
