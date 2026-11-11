import { apiClient } from "@/lib/api/client";
import type {
  ConfirmHoldResponse,
  CreateHoldResponse,
  HoldDetailsResponse,
  MyHoldResponse,
} from "@/types/api";

function studentConfig(studentId: string, signal?: AbortSignal) {
  return {
    headers: {
      "X-Student-Id": studentId,
    },
    signal,
  };
}

export async function createHold(
  classId: string,
  studentId: string,
  signal?: AbortSignal,
): Promise<CreateHoldResponse> {
  const { data } = await apiClient.post<CreateHoldResponse>(
    `/classes/${encodeURIComponent(classId)}/holds`,
    undefined,
    studentConfig(studentId, signal),
  );
  return data;
}

export async function getHold(
  holdId: string,
  signal?: AbortSignal,
): Promise<HoldDetailsResponse> {
  const { data } = await apiClient.get<HoldDetailsResponse>(
    `/holds/${encodeURIComponent(holdId)}`,
    { signal },
  );
  return data;
}

export async function confirmHold(
  holdId: string,
  studentId: string,
): Promise<ConfirmHoldResponse> {
  const { data } = await apiClient.post<ConfirmHoldResponse>(
    `/holds/${encodeURIComponent(holdId)}/confirm`,
    undefined,
    studentConfig(studentId),
  );
  return data;
}

export async function cancelHold(
  holdId: string,
  studentId: string,
): Promise<void> {
  await apiClient.delete(
    `/holds/${encodeURIComponent(holdId)}`,
    studentConfig(studentId),
  );
}

export async function getMyHolds(
  studentId: string,
  signal?: AbortSignal,
): Promise<MyHoldResponse[]> {
  const { data } = await apiClient.get<MyHoldResponse[]>("/me/holds", {
    ...studentConfig(studentId, signal),
  });
  return data;
}
