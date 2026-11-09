import { setupServer } from 'msw/node';
import { defaultHandlers } from './handlers';

/** One MSW server for the whole test run; tests override routes with `server.use(...)`. */
export const server = setupServer(...defaultHandlers);
