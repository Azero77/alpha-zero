import { z } from "zod";
import { createPagedResultSchema, guidSchema, paginationQuerySchema } from "../common";

export const CreateSubjectRequestSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Name is required.")
    .max(100, "Name cannot exceed 100 characters."),
  description: z.string().max(500, "Description cannot exceed 500 characters.").nullable().optional(),
});
export type CreateSubjectRequest = z.infer<typeof CreateSubjectRequestSchema>;

export const CreateSubjectResponseSchema = z.object({
  subjectId: guidSchema,
});
export type CreateSubjectResponse = z.infer<typeof CreateSubjectResponseSchema>;

export const GetSubjectParamsSchema = z.object({
  id: guidSchema,
});

export const ListSubjectsQuerySchema = paginationQuerySchema;

export const SubjectDtoSchema = z.object({
  id: guidSchema,
  name: z.string(),
  description: z.string().nullable().optional(),
});
export type SubjectDto = z.infer<typeof SubjectDtoSchema>;

export const PagedSubjectsResponseSchema = createPagedResultSchema(SubjectDtoSchema);
export type PagedSubjectsResponse = z.infer<typeof PagedSubjectsResponseSchema>;
