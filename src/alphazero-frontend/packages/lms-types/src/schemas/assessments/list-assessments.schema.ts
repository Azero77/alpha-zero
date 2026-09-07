import { z } from "zod";
import { createPagedResultSchema, guidSchema, paginationQuerySchema } from "../common";

export const ListAssessmentsQuerySchema = paginationQuerySchema;

export const AssessmentDtoSchema = z.object({
  id: guidSchema,
  title: z.string(),
  description: z.string().nullable().optional(),
  type: z.string(),
  passingScore: z.number(),
  status: z.string(),
  versionNumber: z.number().int().optional(),
});

export type AssessmentDto = z.infer<typeof AssessmentDtoSchema>;

export const PagedAssessmentsResponseSchema = createPagedResultSchema(AssessmentDtoSchema);
export type PagedAssessmentsResponse = z.infer<typeof PagedAssessmentsResponseSchema>;
