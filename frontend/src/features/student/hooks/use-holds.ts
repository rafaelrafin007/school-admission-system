import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/api/client";
import {
  cancelHold,
  confirmHold,
  createHold,
  getHold,
  getMyHolds,
} from "@/lib/api/holds";
import { queryKeys } from "@/lib/query-keys";
import type { HoldDetailsResponse } from "@/types/api";

const POLL_INTERVAL = 5_000;

function requireStudentId(studentId: string | null): string {
  if (!studentId) {
    throw new ApiError(
      400,
      "Enter a birth registration number before continuing.",
    );
  }

  return studentId;
}

export function useMyHolds(studentId: string | null) {
  return useQuery({
    queryKey: queryKeys.myHolds(studentId ?? ""),
    queryFn: ({ signal }) => getMyHolds(requireStudentId(studentId), signal),
    enabled: Boolean(studentId),
    refetchInterval: POLL_INTERVAL,
    refetchIntervalInBackground: false,
  });
}

export function useHold(holdId: string) {
  return useQuery({
    queryKey: queryKeys.hold(holdId),
    queryFn: ({ signal }) => getHold(holdId, signal),
    enabled: Boolean(holdId),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status && status !== "HELD" ? false : POLL_INTERVAL;
    },
    refetchIntervalInBackground: false,
  });
}

export function useCreateHold(studentId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (classId: string) =>
      createHold(classId, requireStudentId(studentId)),
    onSuccess: async (hold) => {
      queryClient.setQueryData<HoldDetailsResponse>(
        queryKeys.hold(hold.holdId),
        {
          holdId: hold.holdId,
          classId: hold.classId,
          status: hold.status,
          expiresAt: hold.expiresAt,
        },
      );

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.classes }),
        studentId
          ? queryClient.invalidateQueries({
              queryKey: queryKeys.myHolds(studentId),
            })
          : Promise.resolve(),
      ]);
    },
  });
}

export function useConfirmHold(studentId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (holdId: string) =>
      confirmHold(holdId, requireStudentId(studentId)),
    onSuccess: async (response, holdId) => {
      queryClient.setQueryData<HoldDetailsResponse>(
        queryKeys.hold(holdId),
        (current) => (current ? { ...current, status: response.status } : current),
      );

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.classes }),
        studentId
          ? queryClient.invalidateQueries({
              queryKey: queryKeys.myHolds(studentId),
            })
          : Promise.resolve(),
      ]);
    },
  });
}

export function useCancelHold(studentId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (holdId: string) =>
      cancelHold(holdId, requireStudentId(studentId)),
    onSuccess: async (_response, holdId) => {
      queryClient.setQueryData<HoldDetailsResponse>(
        queryKeys.hold(holdId),
        (current) => (current ? { ...current, status: "CANCELLED" } : current),
      );

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.classes }),
        studentId
          ? queryClient.invalidateQueries({
              queryKey: queryKeys.myHolds(studentId),
            })
          : Promise.resolve(),
      ]);
    },
  });
}
