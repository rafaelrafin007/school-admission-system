import { describe, expect, it } from "vitest";
import { studentIdSchema } from "@/features/student/schemas/student-schema";

describe("studentIdSchema", () => {
  it("trims surrounding whitespace and preserves leading zeros", () => {
    const result = studentIdSchema.parse({ studentId: "  0012345  " });

    expect(result.studentId).toBe("0012345");
  });

  it("rejects blank identifiers", () => {
    const result = studentIdSchema.safeParse({ studentId: "   " });

    expect(result.success).toBe(false);
  });

  it("accepts 17 characters and rejects longer identifiers", () => {
    expect(
      studentIdSchema.safeParse({ studentId: "12345678901234567" }).success,
    ).toBe(true);
    expect(
      studentIdSchema.safeParse({ studentId: "123456789012345678" }).success,
    ).toBe(false);
  });
});
