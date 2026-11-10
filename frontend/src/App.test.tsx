import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';
import { AppRoutes } from './AppRoutes';
import { getServerOffsetMs, resetServerOffset } from './api/serverClock';
import { Layout } from './components/Layout';
import { createQueryClient } from './queryClient';
import { StudentProvider } from './student/StudentContext';
import { server } from './test/server';

function renderAt(path: string) {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <StudentProvider>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
        </MemoryRouter>
      </StudentProvider>
    </QueryClientProvider>,
  );
}

describe('routing', () => {
  it.each([
    ['/', 'Classes'],
    ['/admin/classes/new', 'Create a class'],
    ['/applications/0193a6f3-0a11-7c22-8e33-445566778899', 'Application'],
    ['/my-applications', 'My applications'],
  ])('%s renders the "%s" page', (path, heading) => {
    renderAt(path);
    expect(screen.getByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
  });

  it('shows a friendly not-found page for unknown URLs, with a way back', () => {
    renderAt('/nope/deeper');

    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Go to classes' })).toHaveAttribute('href', '/');
  });

  it('shows the header navigation on every page and marks the current one', () => {
    renderAt('/my-applications');

    const nav = screen.getByRole('navigation', { name: 'Main' });
    expect(nav).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Classes' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('link', { name: 'Admin: Create Class' })).toHaveAttribute('href', '/admin/classes/new');
    expect(screen.getByRole('link', { name: 'My Applications' })).toHaveAttribute('aria-current', 'page');
  });

  it('navigates between pages without a reload', async () => {
    renderAt('/');

    await userEvent.click(screen.getByRole('link', { name: 'My Applications' }));

    expect(screen.getByRole('heading', { level: 1, name: 'My applications' })).toBeInTheDocument();
  });
});

describe('ErrorBoundary', () => {
  beforeEach(() => vi.spyOn(console, 'error').mockImplementation(() => {}));
  afterEach(() => vi.restoreAllMocks());

  function Boom(): never {
    throw new Error('render failed');
  }

  it('replaces a crashed page with a friendly message instead of a blank screen', async () => {
    render(
      <QueryClientProvider client={createQueryClient()}>
        <StudentProvider>
          <MemoryRouter initialEntries={['/boom']}>
            <Routes>
              <Route element={<Layout />}>
                <Route path="boom" element={<Boom />} />
                <Route index element={<h1>Classes</h1>} />
              </Route>
            </Routes>
          </MemoryRouter>
        </StudentProvider>
      </QueryClientProvider>,
    );

    expect(screen.getByRole('alert')).toHaveTextContent('Something went wrong on this page.');
    expect(screen.getByRole('button', { name: 'Reload' })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument(); // header still usable

    await userEvent.click(screen.getByRole('link', { name: 'Classes' }));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument(); // recovers after navigating
    expect(screen.getByRole('heading', { level: 1, name: 'Classes' })).toBeInTheDocument();
  });
});

describe('App start-up', () => {
  beforeEach(() => resetServerOffset());
  it('seeds the server clock offset from GET /health once', async () => {
    const serverTime = new Date(Date.now() + 60_000).toISOString().replace(/\.\d{3}Z$/, 'Z');
    let healthCalls = 0;
    server.use(http.get('*/health', () => {
      healthCalls++;
      return HttpResponse.json({ status: 'ok', serverTime });
    }));

    render(<App />);

    await waitFor(() => expect(getServerOffsetMs()).toBeGreaterThan(58_000));
    expect(healthCalls).toBe(1);
  });

  it('still renders, with a friendly message, when the server is unreachable at start-up', async () => {
    server.use(http.all('*', () => HttpResponse.error()));

    render(<App />);

    expect(await screen.findByRole('heading', { level: 1, name: 'Classes' })).toBeInTheDocument();
    // The production query client retries once (~1 s back-off) before reporting the error.
    expect(await screen.findByRole('alert', {}, { timeout: 4_000 })).toHaveTextContent(/Can't reach the server/);
  });
});

describe('query client defaults', () => {
  it('retries queries once, never mutations, and pauses polling in background tabs', () => {
    const defaults = createQueryClient().getDefaultOptions();

    expect(defaults.queries?.retry).toBe(1);
    expect(defaults.queries?.refetchIntervalInBackground).toBe(false);
    expect(defaults.queries?.refetchOnWindowFocus).toBe(true);
    expect(defaults.mutations?.retry).toBe(0);
  });
});
