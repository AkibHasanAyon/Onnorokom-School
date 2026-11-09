import { createContext } from 'react';

/** localStorage key holding the saved birth registration number (R11). */
export const STORAGE_KEY = 'sas.studentId';

export interface StudentContextValue {
  /** The saved, valid 17-digit id; null when none is entered. Used as the X-Student-Id identity. */
  studentId: string | null;
  /** Saves a valid id (persisted, R11). */
  saveStudentId: (id: string) => void;
  /** Forgets the id (e.g. on a shared computer). */
  clearStudentId: () => void;
}

export const StudentContext = createContext<StudentContextValue | null>(null);
