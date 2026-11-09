/**
 * Offset between the server clock and this browser's clock, learned from `serverTime` in API responses.
 * The countdown uses it so a wrong client clock can't show a hold as active after it expired.
 * Precision is about ±1 s (the server sends whole seconds).
 */
let offsetMs = 0;
const listeners = new Set<() => void>();

export function reportServerTime(serverTimeIso: string, receivedAtMs: number = Date.now()): void {
  const server = Date.parse(serverTimeIso);
  if (Number.isNaN(server)) return;
  const next = server - receivedAtMs;
  if (next === offsetMs) return;
  offsetMs = next;
  listeners.forEach((listener) => listener());
}

export const getServerOffsetMs = (): number => offsetMs;

/** Current time on the server's clock (ms since epoch). */
export const serverNow = (): number => Date.now() + offsetMs;

export function subscribeServerOffset(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Test helper. */
export function resetServerOffset(): void {
  offsetMs = 0;
}
