declare global {
  interface Window {
    /** Written at container start by docker/40-runtime-config.sh into /config.js. */
    __APP_CONFIG__?: { API_BASE_URL?: string };
  }
}

export const DEFAULT_API_BASE_URL = 'http://localhost:8080';

/**
 * Backend URL the *browser* calls. Precedence: runtime /config.js → build-time VITE_API_BASE_URL → default.
 * Never `http://backend:8080`: that name only resolves inside the Docker network, not in the browser.
 */
export function resolveApiBaseUrl(runtime: string | undefined, buildTime: string | undefined): string {
  const raw = runtime?.trim() || buildTime?.trim() || DEFAULT_API_BASE_URL;
  return raw.replace(/\/+$/, '');
}

export const API_BASE_URL = resolveApiBaseUrl(
  typeof window === 'undefined' ? undefined : window.__APP_CONFIG__?.API_BASE_URL,
  import.meta.env.VITE_API_BASE_URL as string | undefined,
);
