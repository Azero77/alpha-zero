import { z } from "zod";
import { guidSchema } from "../common";

export const UploadRequestSchema = z.object({
  fileName: z
    .string()
    .trim()
    .min(1, "File name is required.")
    .max(255, "File name cannot exceed 255 characters."),
  contentType: z
    .string()
    .trim()
    .min(1, "Content type is required.")
    .refine((ct) => ct.toLowerCase().startsWith("video/"), {
      message: "Content type must be a valid video MIME type (e.g. video/mp4, video/webm).",
    }),
  fileSizeBytes: z
    .number()
    .positive("File size must be greater than 0 bytes."),
  title: z.string().trim().max(200).nullable().optional(),
  description: z.string().trim().max(2000).nullable().optional(),
});
export type UploadRequest = z.infer<typeof UploadRequestSchema>;

export const UploadResponseSchema = z.object({
  videoId: guidSchema,
  uploadUrl: z.string().url("Upload URL must be a valid URL."),
  key: z.string().min(1, "S3 storage key is required."),
});
export type UploadResponse = z.infer<typeof UploadResponseSchema>;
