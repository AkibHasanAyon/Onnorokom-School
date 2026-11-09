import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { BrowserRouter } from 'react-router';
import { getHealth } from './api/holds';
import { AppRoutes } from './AppRoutes';
import { ErrorBoundary } from './components/ErrorBoundary';
import { createQueryClient } from './queryClient';

/** Learn the server clock offset once at start-up (apiFetch reads serverTime from the response). */
function useSeedServerClock() {
  useEffect(() => {
    getHealth().catch(() => {
      /* offline at start-up: later responses will set the offset */
    });
  }, []);
}

export default function App({ queryClient }: { queryClient?: QueryClient }) {
  const [client] = useState(() => queryClient ?? createQueryClient());
  useSeedServerClock();

  return (
    <ErrorBoundary>
      <QueryClientProvider client={client}>
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
      </QueryClientProvider>
    </ErrorBoundary>
  );
}
