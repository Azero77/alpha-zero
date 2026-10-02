import { z } from "zod";
import { guidSchema } from "../common";

export const AuthorizeResourceParamsSchema = z.object({
  id: guidSchema,
});

export const AuthorizeResourceRequestSchema = z.object({
  resourceId: guidSchema,
  resourceType: z.string().trim().min(1, "Resource type is required."),
});
export type AuthorizeResourceRequest = z.infer<typeof AuthorizeResourceRequestSchema>;

export const DeauthorizeResourceParamsSchema = z.object({
  id: guidSchema,
});

export const DeauthorizeResourceRequestSchema = z.object({
  resourceId: guidSchema,
  resourceType: z.string().trim().min(1, "Resource type is required."),
});
export type DeauthorizeResourceRequest = z.infer<typeof DeauthorizeResourceRequestSchema>;
