import { screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CreateClassForm } from "@/features/admin/components/create-class-form";
import { createClass } from "@/lib/api/classes";
import { renderWithProviders } from "@/test/render";

vi.mock("@/lib/api/classes", () => ({
  getClasses: vi.fn(),
  getClass: vi.fn(),
  createClass: vi.fn(),
}));

const createClassMock = vi.mocked(createClass);

describe("CreateClassForm", () => {
  beforeEach(() => {
    createClassMock.mockReset();
  });

  it("validates required fields before submitting", async () => {
    const { user } = renderWithProviders(<CreateClassForm />);

    await user.click(screen.getByRole("button", { name: "Create class" }));

    expect(await screen.findByText("Enter a class name.")).toBeInTheDocument();
    expect(
      await screen.findByText("Enter the total number of seats."),
    ).toBeInTheDocument();
    expect(createClassMock).not.toHaveBeenCalled();
  });

  it("creates a class and displays a success message", async () => {
    createClassMock.mockResolvedValue({
      id: "class-9",
      name: "Class 9 – Science",
      totalSeats: 48,
    });
    const { user } = renderWithProviders(<CreateClassForm />);

    await user.type(screen.getByLabelText("Class name"), "Class 9 – Science");
    await user.type(screen.getByLabelText("Total seats"), "48");
    await user.click(screen.getByRole("button", { name: "Create class" }));

    await waitFor(() => {
      expect(createClassMock).toHaveBeenCalledWith({
        name: "Class 9 – Science",
        totalSeats: 48,
      });
    });

    expect(await screen.findByText("Class created")).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "View class details" }),
    ).toHaveAttribute("href", "/admin/classes/class-9");
  });
});
