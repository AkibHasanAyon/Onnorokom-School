import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { STORAGE_KEY, StudentContext } from './context';
import { isValidStudentId } from './studentId';

/** localStorage can throw (private mode, disabled storage): identity then lives for the session only. */
function readStored(): string | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return value && isValidStudentId(value) ? value : null;
  } catch {
    return null;
  }
}

function writeStored(value: string | null): void {
  try {
    if (value) localStorage.setItem(STORAGE_KEY, value);
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* storage unavailable: keep working without persistence */
  }
}

/** Client state: the student's identity (React context + localStorage). Server state lives in TanStack Query. */
export function StudentProvider({ children, initialStudentId }: { children: ReactNode; initialStudentId?: string }) {
  const [studentId, setStudentId] = useState<string | null>(() => initialStudentId ?? readStored());

  const saveStudentId = useCallback((id: string) => {
    if (!isValidStudentId(id)) return;
    writeStored(id);
    setStudentId(id);
  }, []);

  const clearStudentId = useCallback(() => {
    writeStored(null);
    setStudentId(null);
  }, []);

  // Keep several open tabs in sync.
  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key === STORAGE_KEY) setStudentId(e.newValue && isValidStudentId(e.newValue) ? e.newValue : null);
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  const value = useMemo(() => ({ studentId, saveStudentId, clearStudentId }), [studentId, saveStudentId, clearStudentId]);
  return <StudentContext.Provider value={value}>{children}</StudentContext.Provider>;
}
