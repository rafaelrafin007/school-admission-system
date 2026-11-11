import type { AxiosRequestConfig } from "axios";
import { afterAll, beforeEach, describe, expect, it } from "vitest";
import { apiClient } from "@/lib/api/client";
import {
  cancelHold,
  confirmHold,
  createHold,
  getHold,
  getMyHolds,
} from "@/lib/api/holds";

describe("hold API headers", () => {
  const originalAdapter = apiClient.defaults.adapter;
  let requests: AxiosRequestConfig[] = [];

  beforeEach(() => {
    requests = [];
    apiClient.defaults.adapter = async (config) => {
      requests.push(config);
      return {
        data: {},
        status: 200,
        statusText: "OK",
        headers: {},
        config,
      };
    };
  });

  afterAll(() => {
    apiClient.defaults.adapter = originalAdapter;
  });

  it("sends the student ID on every student-scoped request", async () => {
    await createHold("class 1", "00123");
    await getMyHolds("00123");
    await confirmHold("hold-1", "00123");
    await cancelHold("hold-1", "00123");

    expect(requests).toHaveLength(4);
    expect(requests.every((request) => request.headers?.["X-Student-Id"] === "00123")).toBe(
      true,
    );
    expect(requests[0].url).toBe("/classes/class%201/holds");
    expect(requests[1].url).toBe("/me/holds");
    expect(requests[2].url).toBe("/holds/hold-1/confirm");
    expect(requests[3].method).toBe("delete");
  });

  it("does not require a student ID when reading a hold", async () => {
    await getHold("hold-1");

    expect(requests).toHaveLength(1);
    expect(requests[0].headers?.["X-Student-Id"]).toBeUndefined();
  });
});
