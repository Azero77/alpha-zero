import { z } from "zod";
import { createPagedResultSchema, guidSchema, paginationQuerySchema } from "../common";

export const ListSubmissionsQuerySchema = paginationQuerySchema.extend({
  assessmentId: guidSchema.optional(),
  status: z.string().optional(),
});

export const SubmissionSummaryDtoSchema = z.object({
  id: guidSchema,
  assessmentId: guidSchema,
  studentId: guidSchema,
  score: z.number().nullable().optional(),
  status: z.string(),
  submittedAt: z.string().datetime().nullable().optional(),
});

export type SubmissionSummaryDto = z.infer<typeof SubmissionSummaryDtoSchema>;

export const PagedSubmissionsResponseSchema = createPagedResultSchema(SubmissionSummaryDtoSchema);
export type PagedSubmissionsResponse = z.infer<typeof PagedSubmissionsResponseSchema>;

export const SubmitAssessmentParamsSchema = z.object({
  submissionId: guidSchema,
});

export const SubmitAssessmentRequestSchema = z.object({
  responses: z.record(z.string(), z.unknown(), {
    message: "Responses cannot be empty.",
  }),
});

export type SubmitAssessmentRequest = z.infer<typeof SubmitAssessmentRequestSchema>;

export const SubmitAssessmentResponseSchema = z.object({
  score: z.number().nullable().optional(),
  status: z.string(),
});

export type SubmitAssessmentResponse = z.infer<typeof SubmitAssessmentResponseSchema>;
