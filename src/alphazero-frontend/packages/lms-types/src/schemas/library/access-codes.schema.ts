import { z } from "zod";
import { createPagedResultSchema, guidSchema, paginationQuerySchema } from "../common";

export const GenerateBatchParamsSchema = z.object({
  libraryId: guidSchema,
});

export const GenerateBatchRequestSchema = z.object({
  quantity: z
    .number()
    .int("Quantity must be an integer.")
    .min(1, "Quantity must be at least 1.")
    .max(1000, "Quantity cannot exceed 1000."),
  resourceId: guidSchema,
  resourceType: z.string().trim().min(1, "Resource type is required."),
  expiresAt: z.string().datetime().nullable().optional(),
  unitPrice: z.number().min(0, "Unit price cannot be negative."),
  revenueShareRate: z
    .number()
    .min(0, "Revenue share rate cannot be negative.")
    .max(1, "Revenue share rate cannot exceed 1 (100%)."),
});
export type GenerateBatchRequest = z.infer<typeof GenerateBatchRequestSchema>;

export const GenerateBatchResponseSchema = z.object({
  batchId: guidSchema,
  generatedCount: z.number().int(),
});
export type GenerateBatchResponse = z.infer<typeof GenerateBatchResponseSchema>;

export const GenerateAdminCodeRequestSchema = z.object({
  resourceId: guidSchema,
  resourceType: z.string().trim().min(1, "Resource type is required."),
  expiresAt: z.string().datetime().nullable().optional(),
  note: z.string().max(500, "Note cannot exceed 500 characters.").nullable().optional(),
});
export type GenerateAdminCodeRequest = z.infer<typeof GenerateAdminCodeRequestSchema>;

export const GenerateAdminCodeResponseSchema = z.object({
  codeId: guidSchema,
  code: z.string(),
});
export type GenerateAdminCodeResponse = z.infer<typeof GenerateAdminCodeResponseSchema>;

export const DistributeBatchParamsSchema = z.object({
  batchId: guidSchema,
});

export const VoidCodeRequestSchema = z.object({
  code: z.string().trim().min(1, "Code is required."),
  reason: z
    .string()
    .trim()
    .min(1, "Reason is required.")
    .max(500, "Reason cannot exceed 500 characters."),
});
export type VoidCodeRequest = z.infer<typeof VoidCodeRequestSchema>;

export const RedeemCodeRequestSchema = z.object({
  code: z.string().trim().min(1, "Access code is required."),
});
export type RedeemCodeRequest = z.infer<typeof RedeemCodeRequestSchema>;

export const RedeemCodeResponseSchema = z.object({
  resourceId: guidSchema,
  resourceType: z.string(),
});
export type RedeemCodeResponse = z.infer<typeof RedeemCodeResponseSchema>;

export const GetRedemptionLogsParamsSchema = z.object({
  libraryId: guidSchema,
});

export const GetRedemptionLogsQuerySchema = paginationQuerySchema;

export const RedemptionLogDtoSchema = z.object({
  id: guidSchema,
  codeId: guidSchema,
  studentId: guidSchema,
  libraryId: guidSchema,
  redeemedAt: z.string().datetime(),
});
export type RedemptionLogDto = z.infer<typeof RedemptionLogDtoSchema>;

export const PagedRedemptionLogsResponseSchema = createPagedResultSchema(RedemptionLogDtoSchema);
export type PagedRedemptionLogsResponse = z.infer<typeof PagedRedemptionLogsResponseSchema>;
