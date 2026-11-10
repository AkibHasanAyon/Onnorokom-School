import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createClass, listClasses } from '../api/classes';
import { cancelHold, confirmHold, createHold, getHold, listMyHolds } from '../api/holds';
import type { HoldDetails } from '../api/types';

/** Query keys (Integration.md §8). */
export const queryKeys = {
  classes: ['classes'] as const,
  myHolds: (studentId: string | null) => ['myHolds', studentId] as const,
  hold: (holdId: string) => ['hold', holdId] as const,
};

export const CLASSES_POLL_MS = 3_000;
export const MY_HOLDS_POLL_MS = 5_000;
export const HOLD_POLL_MS = 5_000;

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

/** Admin: create a class; the class list refreshes immediately on success. */
export function useCreateClass() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createClass,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.classes }),
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
    onSuccess: (hold) => {
      // The application page can show the hold straight away; its own fetch then fills in the rest.
      const seed: HoldDetails = {
        holdId: hold.holdId,
        classId: hold.classId,
        status: hold.status,
        expiresAt: hold.expiresAt,
        className: hold.className,
        createdAt: hold.createdAt,
      };
      queryClient.setQueryData(queryKeys.hold(hold.holdId), seed);
    },
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.classes }),
        queryClient.invalidateQueries({ queryKey: queryKeys.myHolds(studentId) }),
      ]);
    },
  });
}

/** One hold. Polled every 5 s while HELD (another tab may pay or cancel it); stops once final. */
export function useHold(holdId: string) {
  return useQuery({
    queryKey: queryKeys.hold(holdId),
    queryFn: ({ signal }) => getHold(holdId, signal),
    refetchInterval: (query) => (query.state.data?.status === 'HELD' ? HOLD_POLL_MS : false),
  });
}

/** Everything that shows this hold or its seat: the hold itself, seat counts, my applications. */
function useInvalidateHold(holdId: string, studentId: string | null) {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.hold(holdId) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.classes }),
      queryClient.invalidateQueries({ queryKey: queryKeys.myHolds(studentId) }),
    ]);
  };
}

/** Pay (simulated). Refetches whatever the outcome, so a HOLD_EXPIRED 409 flips the page to EXPIRED. */
export function useConfirmHold(holdId: string, studentId: string | null) {
  const invalidate = useInvalidateHold(holdId, studentId);
  return useMutation({
    mutationFn: () => {
      if (!studentId) throw new Error('No student id');
      return confirmHold(holdId, studentId);
    },
    onSettled: invalidate,
  });
}

/** Cancel the application; the seat returns immediately. */
export function useCancelHold(holdId: string, studentId: string | null) {
  const invalidate = useInvalidateHold(holdId, studentId);
  return useMutation({
    mutationFn: () => {
      if (!studentId) throw new Error('No student id');
      return cancelHold(holdId, studentId);
    },
    onSettled: invalidate,
  });
}
