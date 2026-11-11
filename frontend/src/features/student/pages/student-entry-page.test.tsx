import { screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { StudentEntryPage } from "@/features/student/pages/student-entry-page";
import {
  LAST_HOLD_STORAGE_KEY,
  STUDENT_ID_STORAGE_KEY,
} from "@/features/student/hooks/use-student-identity";
import { getMyHolds } from "@/lib/api/holds";
import { renderWithProviders } from "@/test/render";

vi.mock("@/lib/api/holds", () => ({
  createHold: vi.fn(),
  getMyHolds: vi.fn(),
  getHold: vi.fn(),
  confirmHold: vi.fn(),
  cancelHold: vi.fn(),
}));

const getMyHoldsMock = vi.mocked(getMyHolds);

describe("StudentEntryPage", () => {
  beforeEach(() => {
    getMyHoldsMock.mockReset();
    getMyHoldsMock.mockResolvedValue([]);
  });

  it("recovers the student identity and latest application after a refresh", async () => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, "00123");
    window.localStorage.setItem(
      LAST_HOLD_STORAGE_KEY,
      JSON.stringify({ studentId: "00123", holdId: "hold-9" }),
    );

    renderWithProviders(<StudentEntryPage />);

    expect(await screen.findByText("Welcome back.")).toBeInTheDocument();
    expect(screen.getByText("00123")).toBeInTheDocument();
    expect(
      screen.getByRole("link", {
        name: /continue with your latest reservation/i,
      }),
    ).toHaveAttribute("href", "/student/applications/hold-9");
    expect(
      screen.queryByLabelText("Birth registration number"),
    ).not.toBeInTheDocument();
  });
});
