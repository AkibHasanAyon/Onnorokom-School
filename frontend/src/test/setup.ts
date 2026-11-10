import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { resetServerOffset } from '../api/serverClock';
import { server } from './server';

// Any request without a handler fails the test loudly instead of hanging or hitting a real server.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

// Every test starts from the same state: no DOM, default handlers, empty storage, real timers,
// and no learned server-clock offset.
afterEach(() => {
  cleanup();
  server.resetHandlers();
  localStorage.clear();
  vi.useRealTimers();
  resetServerOffset();
});

afterAll(() => server.close());
