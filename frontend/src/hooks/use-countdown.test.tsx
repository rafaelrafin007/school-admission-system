import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useCountdown } from "@/hooks/use-countdown";

describe("useCountdown", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2031-01-01T00:00:00.000Z"));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("counts down using the expiration time and stops at zero", () => {
    const expiresAt = new Date(Date.now() + 65_000).toISOString();
    const { result } = renderHook(() => useCountdown(expiresAt));

    expect(result.current.label).toBe("00:01:05");
    expect(result.current.isExpired).toBe(false);

    act(() => {
      vi.advanceTimersByTime(66_000);
    });

    expect(result.current.label).toBe("00:00:00");
    expect(result.current.isExpired).toBe(true);
  });

  it("treats a missing expiration as unavailable", () => {
    const { result } = renderHook(() => useCountdown(undefined));

    expect(result.current.isValid).toBe(false);
    expect(result.current.isExpired).toBe(true);
  });
});
