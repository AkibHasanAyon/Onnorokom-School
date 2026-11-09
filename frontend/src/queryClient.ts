import { QueryClient } from '@tanstack/react-query';

/**
 * Server state lives only in TanStack Query (Integration.md §8). Polling intervals are set per hook;
 * these are the shared defaults.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: 1,
        refetchOnWindowFocus: true,
        refetchIntervalInBackground: false,
        staleTime: 0,
      },
      mutations: {
        retry: 0, // never repeat apply/pay/cancel automatically
      },
    },
  });
}
