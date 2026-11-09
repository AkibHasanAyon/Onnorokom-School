import { useEffect, useState } from 'react';

/** Re-renders every `intervalMs` and returns Date.now() — for "Updated 5 s ago"-style labels. */
export function useNow(intervalMs: number): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}
