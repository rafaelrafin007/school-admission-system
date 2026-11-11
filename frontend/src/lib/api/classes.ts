import { apiClient } from "@/lib/api/client";
import type {
  ClassResponse,
  CreateClassRequest,
  CreateClassResponse,
} from "@/types/api";

export async function getClasses(
  signal?: AbortSignal,
): Promise<ClassResponse[]> {
  const { data } = await apiClient.get<ClassResponse[]>("/classes", { signal });
  return data;
}

export async function getClass(
  classId: string,
  signal?: AbortSignal,
): Promise<ClassResponse> {
  const { data } = await apiClient.get<ClassResponse>(
    `/classes/${encodeURIComponent(classId)}`,
    { signal },
  );
  return data;
}

export async function createClass(
  request: CreateClassRequest,
): Promise<CreateClassResponse> {
  const { data } = await apiClient.post<CreateClassResponse>(
    "/classes",
    request,
  );
  return data;
}
