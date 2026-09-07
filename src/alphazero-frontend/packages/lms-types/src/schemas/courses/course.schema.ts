import { z } from "zod";
import { createPagedResultSchema, guidSchema, paginationQuerySchema } from "../common";

export const DifficultyLevelEnum = z.enum(["Beginner", "Intermediate", "Advanced"], {
  message: "Difficulty must be Beginner, Intermediate, or Advanced.",
});
export type DifficultyLevel = z.infer<typeof DifficultyLevelEnum>;

export const CourseStatusEnum = z.enum(["Draft", "UnderReview", "Published", "Archived"], {
  message: "Invalid course status.",
});
export type CourseStatus = z.infer<typeof CourseStatusEnum>;

export const CreateCourseRequestSchema = z.object({
  title: z
    .string()
    .trim()
    .min(1, "Title is required.")
    .max(200, "Title cannot exceed 200 characters."),
  description: z
    .string()
    .trim()
    .min(1, "Description is required.")
    .max(2000, "Description cannot exceed 2000 characters."),
  subjectId: guidSchema,
  price: z.number().min(0, "Price cannot be negative."),
  currency: z
    .string()
    .trim()
    .length(3, "Currency must be a 3-character ISO currency code (e.g. USD, SYP).")
    .toUpperCase(),
  difficulty: DifficultyLevelEnum,
  thumbnailUrl: z.string().url().nullable().optional(),
});
export type CreateCourseRequest = z.infer<typeof CreateCourseRequestSchema>;

export const CreateCourseResponseSchema = z.object({
  courseId: guidSchema,
});
export type CreateCourseResponse = z.infer<typeof CreateCourseResponseSchema>;

export const GetCourseParamsSchema = z.object({
  id: guidSchema,
});
export type GetCourseParams = z.infer<typeof GetCourseParamsSchema>;

export const ListCoursesQuerySchema = paginationQuerySchema.extend({
  subjectId: guidSchema.optional(),
  status: CourseStatusEnum.optional(),
});
export type ListCoursesQuery = z.infer<typeof ListCoursesQuerySchema>;

export const CourseSummaryDtoSchema = z.object({
  id: guidSchema,
  title: z.string(),
  description: z.string(),
  subjectId: guidSchema,
  price: z.number(),
  currency: z.string(),
  difficulty: z.string(),
  status: z.string(),
  thumbnailUrl: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});
export type CourseSummaryDto = z.infer<typeof CourseSummaryDtoSchema>;

export const PagedCoursesResponseSchema = createPagedResultSchema(CourseSummaryDtoSchema);
export type PagedCoursesResponse = z.infer<typeof PagedCoursesResponseSchema>;
