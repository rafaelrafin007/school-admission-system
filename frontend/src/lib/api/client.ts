import axios from "axios";
import type { ProblemDetails } from "@/types/api";
import { syncServerClock } from "@/lib/server-clock";

export class ApiError extends Error {
  readonly status: number;
  readonly title?: string;
  readonly detail?: string;
  readonly fieldErrors?: Record<string, string[]>;

  constructor(
    status: number,
    message: string,
    options: {
      title?: string;
      detail?: string;
      fieldErrors?: Record<string, string[]>;
    } = {},
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.title = options.title;
    this.detail = options.detail;
    this.fieldErrors = options.fieldErrors;
  }
}

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "",
  timeout: 15_000,
  headers: {
    Accept: "application/json",
  },
});

apiClient.interceptors.response.use(
  (response) => {
    syncServerClock(response.headers.date);
    return response;
  },
  (error: unknown) => Promise.reject(toApiError(error)),
);

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;

  if (axios.isAxiosError<ProblemDetails>(error)) {
    if (!error.response) {
      return new ApiError(
        0,
        "Unable to connect to the admission service. Check your connection and try again.",
      );
    }

    const { status, data } = error.response;
    const title = typeof data?.title === "string" ? data.title.trim() : "";
    const detail = typeof data?.detail === "string" ? data.detail.trim() : "";
    const fieldErrors = normaliseFieldErrors(data?.errors);

    return new ApiError(
      status,
      title || "The admission service could not complete the request.",
      {
        title: title || undefined,
        detail: detail || undefined,
        fieldErrors,
      },
    );
  }

  return new ApiError(
    0,
    "Unable to connect to the admission service. Check your connection and try again.",
  );
}

export function getApiErrorMessage(error: unknown): string {
  const apiError = toApiError(error);

  if (apiError.status === 0) {
    return "Unable to connect to the admission service. Check your connection and try again.";
  }

  if (apiError.status === 400) {
    return apiError.fieldErrors
      ? "Please check the highlighted fields and try again."
      : apiError.title || "Please check the submitted information and try again.";
  }

  if (apiError.status === 403) {
    return "This application belongs to a different student identifier. Check the birth registration number and try again.";
  }

  if (apiError.status === 404) {
    return "The requested application or class could not be found.";
  }

  if (apiError.status === 409) {
    return (
      apiError.title ||
      apiError.message ||
      "This action conflicts with the current state of the application. Refresh and try again."
    );
  }

  if (apiError.status >= 500) {
    return "The admission service could not complete the request. Please try again in a moment.";
  }

  return apiError.title || "The request could not be completed. Please try again.";
}

export function getApiFieldErrors(
  error: unknown,
): Record<string, string[]> | undefined {
  return toApiError(error).fieldErrors;
}

export function isApiErrorStatus(error: unknown, status: number): boolean {
  return toApiError(error).status === status;
}

function normaliseFieldErrors(
  errors: Record<string, string[]> | undefined,
): Record<string, string[]> | undefined {
  if (!errors) return undefined;

  const entries = Object.entries(errors)
    .filter(([, messages]) => Array.isArray(messages) && messages.length > 0)
    .map(([field, messages]) => [
      field,
      messages.filter((message): message is string => typeof message === "string"),
    ]);

  return entries.length > 0 ? Object.fromEntries(entries) : undefined;
}
