import { z } from "zod";
import { guidSchema } from "../common";

export const GetStreamingInfoParamsSchema = z.object({
  videoId: guidSchema,
});
export type GetStreamingInfoParams = z.infer<typeof GetStreamingInfoParamsSchema>;

export const StreamingInfoDtoSchema = z.object({
  videoId: guidSchema,
  status: z.string(),
  hlsUrl: z.string().url().nullable().optional(),
  dashUrl: z.string().url().nullable().optional(),
  resolutions: z.array(z.string()).default([]),
});
export type StreamingInfoDto = z.infer<typeof StreamingInfoDtoSchema>;

export const GetVideoKeyParamsSchema = z.object({
  videoId: guidSchema,
});
export type GetVideoKeyParams = z.infer<typeof GetVideoKeyParamsSchema>;

export const UpdateVideoInfoParamsSchema = z.object({
  id: guidSchema,
});

export const UpdateVideoInfoRequestSchema = z.object({
  title: z.string().trim().max(200).nullable().optional(),
  description: z.string().trim().max(2000).nullable().optional(),
});
export type UpdateVideoInfoRequest = z.infer<typeof UpdateVideoInfoRequestSchema>;

export const DebugVideoDtoSchema = z.object({
  id: guidSchema,
  fileName: z.string(),
  fileSizeBytes: z.number(),
  status: z.string(),
  createdAt: z.string().datetime().optional(),
});
export type DebugVideoDto = z.infer<typeof DebugVideoDtoSchema>;
