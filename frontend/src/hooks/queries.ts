import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listClasses } from '../api/classes';
import { createHold, listMyHolds } from '../api/holds';

/** Query keys (Integration.md §8). */
export const queryKeys = {
  classes: ['classes'] as const,
  myHolds: (studentId: string | null) => ['myHolds', studentId] as const,
  hold: (holdId: string) => ['hold', holdId] as const,
};

export const CLASSES_POLL_MS = 3_000;
export const MY_HOLDS_POLL_MS = 5_000;

/** All classes with live seat counts, refreshed every 3 s without a reload (R10). */
export function useClasses() {
  return useQuery({
    queryKey: queryKeys.classes,
    queryFn: ({ signal }) => listClasses(signal),
    refetchInterval: CLASSES_POLL_MS,
    placeholderData: keepPreviousData, // keep showing the last numbers if a poll fails
  });
}

/** The current student's applications (all statuses); disabled without a valid id. */
export function useMyHolds(studentId: string | null) {
  return useQuery({
    queryKey: queryKeys.myHolds(studentId),
    queryFn: ({ signal }) => listMyHolds(studentId ?? '', signal),
    enabled: studentId !== null,
    refetchInterval: MY_HOLDS_POLL_MS,
    placeholderData: keepPreviousData,
  });
}

/** Apply for a seat. Seat counts are refreshed whatever the outcome (e.g. after a "full" 409). */
export function useCreateHold(studentId: string | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (classId: string) => {
      if (!studentId) throw new Error('No student id');
      return createHold(classId, studentId);
    },
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.classes }),
        queryClient.invalidateQueries({ queryKey: queryKeys.myHolds(studentId) }),
      ]);
    },
  });
}
