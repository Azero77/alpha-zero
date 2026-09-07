import { z } from "zod";
import { createPagedResultSchema, guidSchema, paginationQuerySchema } from "../common";

export const TenantStatusEnum = z.enum(["Active", "Suspended", "Pending"], {
  message: "Status must be Active, Suspended, or Pending.",
});
export type TenantStatus = z.infer<typeof TenantStatusEnum>;

export const GetTenantParamsSchema = z.object({
  id: guidSchema,
});

export const ListTenantsQuerySchema = paginationQuerySchema;

export const LookupTenantQuerySchema = z.object({
  identifier: z.string().trim().optional(),
  domain: z.string().trim().optional(),
});
export type LookupTenantQuery = z.infer<typeof LookupTenantQuerySchema>;

export const UpdateTenantParamsSchema = z.object({
  id: guidSchema,
});

export const UpdateTenantRequestSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Tenant name is required.")
    .max(100, "Tenant name cannot exceed 100 characters."),
  domain: z
    .string()
    .trim()
    .min(1, "Domain is required.")
    .max(100, "Domain cannot exceed 100 characters."),
  status: TenantStatusEnum,
});
export type UpdateTenantRequest = z.infer<typeof UpdateTenantRequestSchema>;

export const DeleteTenantParamsSchema = z.object({
  id: guidSchema,
});

export const TenantDtoSchema = z.object({
  id: guidSchema,
  identifier: z.string(),
  name: z.string(),
  domain: z.string(),
  status: z.string(),
  createdAt: z.string().datetime().optional(),
});
export type TenantDto = z.infer<typeof TenantDtoSchema>;

export const PagedTenantsResponseSchema = createPagedResultSchema(TenantDtoSchema);
export type PagedTenantsResponse = z.infer<typeof PagedTenantsResponseSchema>;

export const TenantLookupDtoSchema = z.object({
  id: guidSchema,
  identifier: z.string(),
  name: z.string(),
  domain: z.string(),
  status: z.string(),
  branding: z.record(z.string(), z.unknown()).nullable().optional(),
});
export type TenantLookupDto = z.infer<typeof TenantLookupDtoSchema>;
