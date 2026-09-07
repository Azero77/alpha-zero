import { z } from "zod";
import { createPagedResultSchema, guidSchema, paginationQuerySchema } from "../common";

export const CreateLibraryRequestSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Library name is required.")
    .max(200, "Library name cannot exceed 200 characters."),
  physicalAddress: z
    .string()
    .trim()
    .min(1, "Physical address is required.")
    .max(500, "Physical address cannot exceed 500 characters."),
  contactPhone: z
    .string()
    .trim()
    .min(1, "Contact phone is required.")
    .max(30, "Contact phone cannot exceed 30 characters."),
  defaultRevenueShareRate: z
    .number()
    .min(0, "Default revenue share rate cannot be negative.")
    .max(1, "Default revenue share rate cannot exceed 1 (100%)."),
});
export type CreateLibraryRequest = z.infer<typeof CreateLibraryRequestSchema>;

export const CreateLibraryResponseSchema = z.object({
  libraryId: guidSchema,
});
export type CreateLibraryResponse = z.infer<typeof CreateLibraryResponseSchema>;

export const GetLibraryParamsSchema = z.object({
  id: guidSchema,
});

export const ListLibrariesQuerySchema = paginationQuerySchema;

export const UpdateLibraryParamsSchema = z.object({
  id: guidSchema,
});

export const UpdateLibraryRequestSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Library name is required.")
    .max(200, "Library name cannot exceed 200 characters."),
  physicalAddress: z
    .string()
    .trim()
    .min(1, "Physical address is required.")
    .max(500, "Physical address cannot exceed 500 characters."),
  contactPhone: z
    .string()
    .trim()
    .min(1, "Contact phone is required.")
    .max(30, "Contact phone cannot exceed 30 characters."),
  defaultRevenueShareRate: z
    .number()
    .min(0, "Default revenue share rate cannot be negative.")
    .max(1, "Default revenue share rate cannot exceed 1 (100%)."),
  isActive: z.boolean(),
});
export type UpdateLibraryRequest = z.infer<typeof UpdateLibraryRequestSchema>;

export const DeleteLibraryParamsSchema = z.object({
  id: guidSchema,
});

export const LibraryDtoSchema = z.object({
  id: guidSchema,
  name: z.string(),
  physicalAddress: z.string(),
  contactPhone: z.string(),
  defaultRevenueShareRate: z.number(),
  isActive: z.boolean(),
  createdAt: z.string().datetime().optional(),
});
export type LibraryDto = z.infer<typeof LibraryDtoSchema>;

export const PagedLibrariesResponseSchema = createPagedResultSchema(LibraryDtoSchema);
export type PagedLibrariesResponse = z.infer<typeof PagedLibrariesResponseSchema>;
