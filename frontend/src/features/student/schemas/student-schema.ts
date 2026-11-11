import { z } from "zod";

export const studentIdSchema = z.object({
  studentId: z
    .string()
    .trim()
    .min(1, "Enter your birth registration number.")
    .max(17, "The birth registration number must not exceed 17 characters."),
});

export type StudentIdFormValues = z.infer<typeof studentIdSchema>;
