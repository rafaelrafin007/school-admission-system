import { z } from "zod";

export const classFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Enter a class name.")
    .max(160, "Class name must not exceed 160 characters."),
  totalSeats: z
    .string()
    .trim()
    .min(1, "Enter the total number of seats.")
    .regex(/^\d+$/, "Enter seats as a whole number.")
    .transform((value) => Number(value))
    .refine((value) => value >= 1, "Total seats must be at least 1.")
    .refine(
      (value) => value <= 2_147_483_647,
      "Total seats is larger than the allowed limit.",
    ),
});

export type ClassFormInput = z.input<typeof classFormSchema>;
export type ClassFormValues = z.output<typeof classFormSchema>;
