import { z } from "zod";
import { guidSchema } from "../common";

export const EnrollInCourseRequestSchema = z.object({
  courseId: guidSchema,
  planId: guidSchema,
  libraryCode: z.string().trim().nullable().optional(),
});
export type EnrollInCourseRequest = z.infer<typeof EnrollInCourseRequestSchema>;

export const EnrollInCourseResponseSchema = z.object({
  enrollmentId: guidSchema,
});
export type EnrollInCourseResponse = z.infer<typeof EnrollInCourseResponseSchema>;

export const GetEnrollmentParamsSchema = z.object({
  id: guidSchema,
});

export const CompleteItemParamsSchema = z.object({
  enrollmentId: guidSchema,
});

export const CompleteItemRequestSchema = z.object({
  bitIndex: z.number().int().min(0, "Bit index must be a non-negative integer."),
});
export type CompleteItemRequest = z.infer<typeof CompleteItemRequestSchema>;

export const EnrollmentDtoSchema = z.object({
  id: guidSchema,
  courseId: guidSchema,
  studentId: guidSchema,
  planId: guidSchema,
  status: z.string(),
  enrolledAt: z.string().datetime().optional(),
  progressMask: z.string().optional(),
  completionPercentage: z.number().min(0).max(100).optional(),
});
export type EnrollmentDto = z.infer<typeof EnrollmentDtoSchema>;
