import { useContext } from 'react';
import { StudentContext, type StudentContextValue } from './context';

export function useStudent(): StudentContextValue {
  const ctx = useContext(StudentContext);
  if (!ctx) throw new Error('useStudent must be used inside <StudentProvider>');
  return ctx;
}
