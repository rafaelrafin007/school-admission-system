import { screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { StudentApplicationsPage } from "@/features/student/pages/student-applications-page";
import { STUDENT_ID_STORAGE_KEY } from "@/features/student/hooks/use-student-identity";
import { getMyHolds } from "@/lib/api/holds";
import { renderWithProviders } from "@/test/render";
import { makeMyHold } from "@/test/fixtures";

vi.mock("@/lib/api/holds", () => ({
  createHold: vi.fn(),
  getMyHolds: vi.fn(),
  getHold: vi.fn(),
  confirmHold: vi.fn(),
  cancelHold: vi.fn(),
}));

const getMyHoldsMock = vi.mocked(getMyHolds);

describe("StudentApplicationsPage", () => {
  beforeEach(() => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, "00123");
    getMyHoldsMock.mockReset();
  });

  it("lists application history with statuses", async () => {
    getMyHoldsMock.mockResolvedValue([
      makeMyHold(),
      makeMyHold({
        holdId: "hold-2",
        className: "Class 7 – Afternoon",
        status: "CONFIRMED",
      }),
    ]);

    renderWithProviders(<StudentApplicationsPage />);

    expect(await screen.findByText("Class 6 – Morning")).toBeInTheDocument();
    expect(screen.getByText("Class 7 – Afternoon")).toBeInTheDocument();
    expect(screen.getByText("Held")).toBeInTheDocument();
    expect(screen.getByText("Confirmed")).toBeInTheDocument();
    expect(
      screen.getAllByRole("link", { name: /view application/i }),
    ).toHaveLength(2);
  });

  it("shows an empty state when there are no applications", async () => {
    getMyHoldsMock.mockResolvedValue([]);

    renderWithProviders(<StudentApplicationsPage />);

    expect(await screen.findByText("No applications yet")).toBeInTheDocument();
  });
});
