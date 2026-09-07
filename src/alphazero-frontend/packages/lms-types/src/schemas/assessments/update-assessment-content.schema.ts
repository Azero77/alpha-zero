import { z } from "zod";
import { guidSchema } from "../common";

export const UpdateAssessmentContentParamsSchema = z.object({
  assessmentId: guidSchema,
});

export const UpdateAssessmentContentRequestSchema = z.object({
  content: z.record(z.string(), z.unknown(), {
    message: "Content cannot be null.",
  }),
});

export type UpdateAssessmentContentRequest = z.infer<typeof UpdateAssessmentContentRequestSchema>;
