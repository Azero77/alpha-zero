import { z } from "zod";
import { guidSchema } from "../common";

export const CreateTenantRequestSchema = z.object({
  identifier: z
    .string()
    .trim()
    .regex(/^[a-z0-9-]+$/, "Identifier can only contain lowercase alphanumeric characters and hyphens.")
    .max(50, "Identifier cannot exceed 50 characters."),
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
  adminEmail: z.string().trim().email("Invalid admin email address format."),
  adminPassword: z.string().min(8, "Password must be at least 8 characters long."),
  adminFullName: z
    .string()
    .trim()
    .min(1, "Admin full name is required.")
    .max(100, "Admin full name cannot exceed 100 characters."),
  adminPhone: z.string().trim().min(1, "Admin phone number is required."),
});
export type CreateTenantRequest = z.infer<typeof CreateTenantRequestSchema>;

export const CreateTenantResponseSchema = z.object({
  tenantId: guidSchema,
});
export type CreateTenantResponse = z.infer<typeof CreateTenantResponseSchema>;
