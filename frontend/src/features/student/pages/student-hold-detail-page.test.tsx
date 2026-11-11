import { Route, Routes } from "react-router-dom";
import { screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { StudentHoldDetailPage } from "@/features/student/pages/student-hold-detail-page";
import { STUDENT_ID_STORAGE_KEY } from "@/features/student/hooks/use-student-identity";
import { getClasses } from "@/lib/api/classes";
import { cancelHold, confirmHold, getHold } from "@/lib/api/holds";
import { renderWithProviders } from "@/test/render";
import { makeClass, makeHoldDetails } from "@/test/fixtures";

vi.mock("@/lib/api/classes", () => ({
  getClasses: vi.fn(),
  getClass: vi.fn(),
  createClass: vi.fn(),
}));

vi.mock("@/lib/api/holds", () => ({
  createHold: vi.fn(),
  getMyHolds: vi.fn(),
  getHold: vi.fn(),
  confirmHold: vi.fn(),
  cancelHold: vi.fn(),
}));

const getClassesMock = vi.mocked(getClasses);
const getHoldMock = vi.mocked(getHold);
const confirmHoldMock = vi.mocked(confirmHold);
const cancelHoldMock = vi.mocked(cancelHold);

const detailRoutes = (
  <Routes>
    <Route
      path="/student/applications/:holdId"
      element={<StudentHoldDetailPage />}
    />
  </Routes>
);

describe("StudentHoldDetailPage", () => {
  beforeEach(() => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, "00123");
    getClassesMock.mockReset();
    getHoldMock.mockReset();
    confirmHoldMock.mockReset();
    cancelHoldMock.mockReset();
    getClassesMock.mockResolvedValue([makeClass()]);
    getHoldMock.mockResolvedValue(makeHoldDetails());
  });

  it("confirms a held seat after an explicit dialog", async () => {
    confirmHoldMock.mockResolvedValue({
      holdId: "hold-1",
      status: "CONFIRMED",
    });
    const { user } = renderWithProviders(detailRoutes, {
      route: "/student/applications/hold-1",
    });

    expect(await screen.findByText("Reservation active")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Confirm admission" }));

    const dialog = await screen.findByRole("dialog");
    await user.click(
      within(dialog).getByRole("button", { name: "Confirm admission" }),
    );

    await waitFor(() => {
      expect(confirmHoldMock).toHaveBeenCalledWith("hold-1", "00123");
    });
    expect(await screen.findByText("Admission confirmed")).toBeInTheDocument();
  });

  it("cancels a held seat after an explicit dialog", async () => {
    cancelHoldMock.mockResolvedValue(undefined);
    const { user } = renderWithProviders(detailRoutes, {
      route: "/student/applications/hold-1",
    });

    expect(await screen.findByText("Reservation active")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Cancel reservation" }));

    const dialog = await screen.findByRole("dialog");
    await user.click(
      within(dialog).getByRole("button", { name: "Cancel reservation" }),
    );

    await waitFor(() => {
      expect(cancelHoldMock).toHaveBeenCalledWith("hold-1", "00123");
    });
    expect(await screen.findByText("Reservation cancelled")).toBeInTheDocument();
  });

  it("does not allow confirmation after the countdown has expired", async () => {
    getHoldMock.mockResolvedValue(
      makeHoldDetails({
        expiresAt: new Date(Date.now() - 1_000).toISOString(),
      }),
    );

    renderWithProviders(detailRoutes, {
      route: "/student/applications/hold-1",
    });

    expect(
      await screen.findByText("The reservation time has ended"),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Confirm admission" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Cancel reservation" }),
    ).not.toBeInTheDocument();
  });
});
