import { z } from "zod";

/**
 * Standard GUID / UUID validator accepting any 8-4-4-4-12 hex GUID (C# compatible)
 */
export const guidSchema = z
  .string()
  .trim()
  .regex(
    /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/,
    "Invalid GUID/UUID format."
  );

/**
 * Standard pagination query parameters for GET list requests
 */
export const paginationQuerySchema = z.object({
  page: z.coerce.number().int().min(1, "Page must be at least 1.").default(1),
  perPage: z.coerce
    .number()
    .int()
    .min(1, "PerPage must be at least 1.")
    .max(100, "PerPage cannot exceed 100.")
    .default(10),
});

export type PaginationQuery = z.infer<typeof paginationQuerySchema>;

/**
 * Generic factory for PagedResult<T> responses matching ASP.NET PagedResult
 */
export const createPagedResultSchema = <T extends z.ZodTypeAny>(itemSchema: T) =>
  z.object({
    items: z.array(itemSchema).default([]),
    totalCount: z.number().int().min(0),
    currentPage: z.number().int().min(1),
    pageSize: z.number().int().min(1),
    totalPages: z.number().int().min(0),
    hasNextPage: z.boolean().optional(),
    hasPreviousPage: z.boolean().optional(),
  });
