import { z } from "zod";
import { guidSchema } from "../common";

export const AssessmentTypeEnum = z.enum(["MCQ", "Handwritten", "Hybrid"], {
  message: "Type must be MCQ, Handwritten, or Hybrid.",
});

export type AssessmentType = z.infer<typeof AssessmentTypeEnum>;

export const CreateAssessmentRequestSchema = z.object({
  title: z
    .string()
    .trim()
    .min(1, "Title is required.")
    .max(256, "Title cannot exceed 256 characters."),
  description: z.string().nullable().optional(),
  type: AssessmentTypeEnum,
  passingScore: z.number().min(0, "Passing score cannot be negative."),
  initialContent: z.record(z.string(), z.unknown()).nullable().optional(),
});

export type CreateAssessmentRequest = z.infer<typeof CreateAssessmentRequestSchema>;

export const CreateAssessmentResponseSchema = z.object({
  id: guidSchema,
});

export type CreateAssessmentResponse = z.infer<typeof CreateAssessmentResponseSchema>;
