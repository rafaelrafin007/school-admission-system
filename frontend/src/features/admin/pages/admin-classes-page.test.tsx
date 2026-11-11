import { screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AdminClassesPage } from "@/features/admin/pages/admin-classes-page";
import { getClasses } from "@/lib/api/classes";
import { ApiError } from "@/lib/api/client";
import { renderWithProviders } from "@/test/render";
import { makeClass } from "@/test/fixtures";

vi.mock("@/lib/api/classes", () => ({
  getClasses: vi.fn(),
  getClass: vi.fn(),
  createClass: vi.fn(),
}));

const getClassesMock = vi.mocked(getClasses);

describe("AdminClassesPage", () => {
  beforeEach(() => {
    getClassesMock.mockReset();
  });

  it("renders live class availability", async () => {
    getClassesMock.mockResolvedValue([makeClass()]);

    renderWithProviders(<AdminClassesPage />);

    expect(await screen.findByText("Class 6 – Morning")).toBeInTheDocument();
    expect(screen.getByText("Seats available")).toBeInTheDocument();
    expect(screen.getByText(/27 available/)).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /view class details/i }),
    ).toHaveAttribute("href", "/admin/classes/class-1");
  });

  it("shows an empty state when no classes exist", async () => {
    getClassesMock.mockResolvedValue([]);

    renderWithProviders(<AdminClassesPage />);

    expect(
      await screen.findByText("No classes have been created"),
    ).toBeInTheDocument();
  });

  it("shows a recoverable error state when the API fails", async () => {
    getClassesMock.mockRejectedValue(new ApiError(500, "Server unavailable"));

    renderWithProviders(<AdminClassesPage />);

    expect(
      await screen.findByText("Classes could not be loaded"),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "The admission service could not complete the request. Please try again in a moment.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /try again/i })).toBeInTheDocument();
  });
});
