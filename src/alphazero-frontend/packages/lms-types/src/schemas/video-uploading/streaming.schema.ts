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

export const CreatePlaybackSessionParamsSchema = z.object({
  videoId: guidSchema,
});
export type CreatePlaybackSessionParams = z.infer<typeof CreatePlaybackSessionParamsSchema>;

export const CreatePlaybackSessionRequestSchema = z.object({
  courseId: guidSchema.optional(),
  itemId: guidSchema.optional(),
});
export type CreatePlaybackSessionRequest = z.infer<typeof CreatePlaybackSessionRequestSchema>;

export const EdgeCapabilityDtoSchema = z.object({
  cookieName: z.string(),
  cookieValue: z.string(),
  domain: z.string().nullable().optional(),
  path: z.string(),
  expiresAt: z.string().datetime(),
  httpOnly: z.boolean(),
  secure: z.boolean(),
  sameSite: z.string(),
});
export type EdgeCapabilityDto = z.infer<typeof EdgeCapabilityDtoSchema>;

export const WatermarkContextDtoSchema = z.object({
  userId: z.string(),
  userName: z.string(),
  userPhone: z.string().nullable().optional(),
  tenantId: z.string(),
  issuedAt: z.string().datetime(),
});
export type WatermarkContextDto = z.infer<typeof WatermarkContextDtoSchema>;

export const PlaybackSessionDtoSchema = z.object({
  sessionId: guidSchema,
  videoId: guidSchema,
  masterManifestUrl: z.string(),
  edgeCapability: EdgeCapabilityDtoSchema,
  watermarkContext: WatermarkContextDtoSchema,
});
export type PlaybackSessionDto = z.infer<typeof PlaybackSessionDtoSchema>;

