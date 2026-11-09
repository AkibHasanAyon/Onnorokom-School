import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { AppRoutes } from '../AppRoutes';
import { StudentProvider } from '../student/StudentContext';

/** Renders the real routes at `path`, optionally with a saved student id (no retries, fresh cache). */
export function renderApp(path: string, { studentId }: { studentId?: string } = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchIntervalInBackground: true }, mutations: { retry: false } },
  });
  const result = render(
    <QueryClientProvider client={queryClient}>
      <StudentProvider initialStudentId={studentId}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
        </MemoryRouter>
      </StudentProvider>
    </QueryClientProvider>,
  );
  return { ...result, queryClient };
}
