import { z } from "zod";
import { guidSchema } from "../common";

export const AddSectionParamsSchema = z.object({
  courseId: guidSchema,
});

export const AddSectionRequestSchema = z.object({
  title: z
    .string()
    .trim()
    .min(1, "Section title is required.")
    .max(200, "Section title cannot exceed 200 characters."),
});
export type AddSectionRequest = z.infer<typeof AddSectionRequestSchema>;

export const AddSectionResponseSchema = z.object({
  sectionId: guidSchema,
});
export type AddSectionResponse = z.infer<typeof AddSectionResponseSchema>;

export const AddLessonParamsSchema = z.object({
  courseId: guidSchema,
  sectionId: guidSchema,
});

export const AddLessonRequestSchema = z.object({
  title: z
    .string()
    .trim()
    .min(1, "Lesson title is required.")
    .max(200, "Lesson title cannot exceed 200 characters."),
  videoId: guidSchema,
  duration: z.union([z.string(), z.number()]),
  isFreePreview: z.boolean().default(false),
});
export type AddLessonRequest = z.infer<typeof AddLessonRequestSchema>;

export const AddLessonResponseSchema = z.object({
  lessonId: guidSchema,
});
export type AddLessonResponse = z.infer<typeof AddLessonResponseSchema>;

export const AddQuizParamsSchema = z.object({
  courseId: guidSchema,
  sectionId: guidSchema,
});

export const AddQuizRequestSchema = z.object({
  assessmentId: guidSchema,
  title: z
    .string()
    .trim()
    .min(1, "Quiz title is required.")
    .max(200, "Quiz title cannot exceed 200 characters."),
});
export type AddQuizRequest = z.infer<typeof AddQuizRequestSchema>;

export const AddQuizResponseSchema = z.object({
  quizId: guidSchema,
});
export type AddQuizResponse = z.infer<typeof AddQuizResponseSchema>;

export const ReorderSectionsParamsSchema = z.object({
  courseId: guidSchema,
});

export const ReorderSectionsRequestSchema = z.object({
  sectionIds: z.array(guidSchema).min(1, "At least one section ID is required."),
});
export type ReorderSectionsRequest = z.infer<typeof ReorderSectionsRequestSchema>;

export const ReorderItemsParamsSchema = z.object({
  courseId: guidSchema,
  sectionId: guidSchema,
});

export const ReorderItemsRequestSchema = z.object({
  itemIds: z.array(guidSchema).min(1, "At least one item ID is required."),
});
export type ReorderItemsRequest = z.infer<typeof ReorderItemsRequestSchema>;
