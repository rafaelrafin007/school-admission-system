import { Route, Routes } from "react-router-dom";
import { screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { StudentApplyPage } from "@/features/student/pages/student-apply-page";
import {
  LAST_HOLD_STORAGE_KEY,
  STUDENT_ID_STORAGE_KEY,
} from "@/features/student/hooks/use-student-identity";
import { getClasses } from "@/lib/api/classes";
import { createHold, getMyHolds } from "@/lib/api/holds";
import { ApiError } from "@/lib/api/client";
import { renderWithProviders } from "@/test/render";
import { makeClass } from "@/test/fixtures";

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
const getMyHoldsMock = vi.mocked(getMyHolds);
const createHoldMock = vi.mocked(createHold);

const routes = (
  <Routes>
    <Route path="/student/apply" element={<StudentApplyPage />} />
    <Route
      path="/student/applications/:holdId"
      element={<div>Application destination</div>}
    />
  </Routes>
);

describe("StudentApplyPage", () => {
  beforeEach(() => {
    window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, "00123");
    getClassesMock.mockReset();
    getMyHoldsMock.mockReset();
    createHoldMock.mockReset();
    getClassesMock.mockResolvedValue([makeClass()]);
    getMyHoldsMock.mockResolvedValue([]);
  });

  it("creates a hold with the student ID and remembers the application", async () => {
    createHoldMock.mockResolvedValue({
      holdId: "hold-9",
      classId: "class-1",
      studentId: "00123",
      status: "HELD",
      expiresAt: new Date(Date.now() + 600_000).toISOString(),
    });
    const { user } = renderWithProviders(routes, {
      route: "/student/apply",
    });

    expect(await screen.findByText("Class 6 – Morning")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Reserve a seat" }));

    await waitFor(() => {
      expect(createHoldMock).toHaveBeenCalledWith("class-1", "00123");
    });
    expect(await screen.findByText("Application destination")).toBeInTheDocument();
    expect(
      JSON.parse(
        window.localStorage.getItem(LAST_HOLD_STORAGE_KEY) ?? "null",
      ),
    ).toEqual({
      studentId: "00123",
      holdId: "hold-9",
    });
  });

  it("shows the backend conflict message when a seat is already held", async () => {
    createHoldMock.mockRejectedValue(
      new ApiError(409, "A seat is already held for this class."),
    );
    const { user } = renderWithProviders(routes, {
      route: "/student/apply",
    });

    expect(await screen.findByText("Class 6 – Morning")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Reserve a seat" }));

    expect(
      await screen.findByText("A seat is already held for this class."),
    ).toBeInTheDocument();
    expect(
      screen.getByText("The seat could not be reserved"),
    ).toBeInTheDocument();
  });
});
