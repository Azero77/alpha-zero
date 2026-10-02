import { z } from "zod";
import { guidSchema } from "../common";

export const GetAssessmentParamsSchema = z.object({
  id: guidSchema,
});

export const GetAssessmentQuerySchema = z.object({
  version: z.coerce.number().int().positive().optional(),
});

export const AssessmentDetailsDtoSchema = z.object({
  id: guidSchema,
  title: z.string(),
  description: z.string().nullable().optional(),
  type: z.string(),
  passingScore: z.number(),
  status: z.string(),
  versionNumber: z.number().int(),
  content: z.record(z.string(), z.unknown()).nullable().optional(),
});

export type AssessmentDetailsDto = z.infer<typeof AssessmentDetailsDtoSchema>;
