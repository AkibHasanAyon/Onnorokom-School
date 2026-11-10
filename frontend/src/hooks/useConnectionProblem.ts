import { useQueryClient, type QueryCache } from '@tanstack/react-query';
import { useCallback, useSyncExternalStore } from 'react';
import { ApiError } from '../api/client';

export type ConnectionProblemKind = 'network' | 'server';

export interface ConnectionProblem {
  kind: ConnectionProblemKind;
  /** When the oldest data on screen was last refreshed successfully (ms), or null if it never loaded. */
  lastSuccessAt: number | null;
}

/** Errors that mean "the server can't be used right now" (as opposed to e.g. a 404 for one item). */
export function connectionProblemKind(error: unknown): ConnectionProblemKind | null {
  if (!(error instanceof ApiError)) return null;
  if (error.kind === 'network' || error.kind === 'timeout') return 'network';
  if (error.status !== null && error.status >= 500) return 'server';
  return null;
}

/**
 * A primitive snapshot (`kind:lastSuccessAt`, or '' when all is well) so useSyncExternalStore can
 * compare it cheaply. Only queries shown on the current screen (with observers) count, so a failure on a
 * page you left doesn't keep the banner up. Unreachable-server problems win over server errors.
 */
function snapshot(cache: QueryCache): string {
  let kind: ConnectionProblemKind | null = null;
  let oldest: number | null = null;
  for (const query of cache.getAll()) {
    if (query.getObserversCount() === 0 || query.state.status !== 'error') continue;
    const k = connectionProblemKind(query.state.error);
    if (!k) continue;
    kind = kind === 'network' ? 'network' : k;
    const updated = query.state.dataUpdatedAt;
    if (updated > 0) oldest = oldest === null ? updated : Math.min(oldest, updated);
  }
  return kind ? `${kind}:${oldest ?? ''}` : '';
}

/** Whether polling is currently failing because the server is unreachable or erroring (Design.md §2). */
export function useConnectionProblem(): ConnectionProblem | null {
  const cache = useQueryClient().getQueryCache();
  const subscribe = useCallback((onChange: () => void) => cache.subscribe(onChange), [cache]);
  const value = useSyncExternalStore(subscribe, () => snapshot(cache));
  if (!value) return null;
  const [kind, at] = value.split(':');
  return { kind: kind as ConnectionProblemKind, lastSuccessAt: at ? Number(at) : null };
}
