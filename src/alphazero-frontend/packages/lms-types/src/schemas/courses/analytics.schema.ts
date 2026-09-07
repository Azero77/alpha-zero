import { z } from "zod";
import { createPagedResultSchema, guidSchema, paginationQuerySchema } from "../common";

export const GetCourseAnalyticsParamsSchema = z.object({
  courseId: guidSchema,
});

export const ListStudentProgressParamsSchema = z.object({
  courseId: guidSchema,
});

export const ListStudentProgressQuerySchema = paginationQuerySchema;

export const StudentProgressDtoSchema = z.object({
  studentId: guidSchema,
  studentName: z.string(),
  completionPercentage: z.number(),
  completedItemsCount: z.number().int(),
  totalItemsCount: z.number().int(),
  lastActivityAt: z.string().datetime().nullable().optional(),
});
export type StudentProgressDto = z.infer<typeof StudentProgressDtoSchema>;

export const PagedStudentProgressResponseSchema = createPagedResultSchema(StudentProgressDtoSchema);
export type PagedStudentProgressResponse = z.infer<typeof PagedStudentProgressResponseSchema>;

export const GetStudentDashboardParamsSchema = z.object({
  studentId: guidSchema,
});

export const StudentDashboardDtoSchema = z.object({
  studentId: guidSchema,
  enrolledCoursesCount: z.number().int(),
  completedCoursesCount: z.number().int(),
  inProgressCoursesCount: z.number().int(),
  enrollments: z.array(z.record(z.string(), z.unknown())).default([]),
});
export type StudentDashboardDto = z.infer<typeof StudentDashboardDtoSchema>;
