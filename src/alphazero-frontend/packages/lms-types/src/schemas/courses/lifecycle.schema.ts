import { z } from "zod";
import { guidSchema } from "../common";

export const SubmitForReviewParamsSchema = z.object({
  courseId: guidSchema,
});

export const ApproveCourseParamsSchema = z.object({
  courseId: guidSchema,
});

export const RejectCourseParamsSchema = z.object({
  courseId: guidSchema,
});

export const RejectCourseRequestSchema = z.object({
  reason: z.string().trim().min(1, "Rejection reason is required."),
});
export type RejectCourseRequest = z.infer<typeof RejectCourseRequestSchema>;

export const PublishCourseParamsSchema = z.object({
  courseId: guidSchema,
});
