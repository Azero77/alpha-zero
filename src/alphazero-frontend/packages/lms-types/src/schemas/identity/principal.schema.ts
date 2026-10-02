import { z } from "zod";
import { guidSchema } from "../common";
import { PolicyStatementDtoSchema } from "./policy.schema";

export const PrincipalTypeEnum = z.enum(["User", "Role", "Group"], {
  message: "Principal type must be User, Role, or Group.",
});
export type PrincipalType = z.infer<typeof PrincipalTypeEnum>;

export const CreatePrincipalRequestSchema = z.object({
  userId: guidSchema,
  type: PrincipalTypeEnum,
});
export type CreatePrincipalRequest = z.infer<typeof CreatePrincipalRequestSchema>;

export const CreatePrincipalResponseSchema = z.object({
  principalId: guidSchema,
});
export type CreatePrincipalResponse = z.infer<typeof CreatePrincipalResponseSchema>;

export const AttachInlinePolicyParamsSchema = z.object({
  principalId: guidSchema,
});

export const AttachInlinePolicyRequestSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Policy name is required.")
    .max(100, "Policy name cannot exceed 100 characters."),
  statements: z.array(PolicyStatementDtoSchema).min(1, "At least one statement is required."),
});
export type AttachInlinePolicyRequest = z.infer<typeof AttachInlinePolicyRequestSchema>;

export const AttachPolicyResponseSchema = z.object({
  policyId: guidSchema,
});
export type AttachPolicyResponse = z.infer<typeof AttachPolicyResponseSchema>;

export const DetachInlinePolicyParamsSchema = z.object({
  principalId: guidSchema,
  policyId: guidSchema,
});

export const AttachManagedPolicyParamsSchema = z.object({
  principalId: guidSchema,
  managedPolicyId: guidSchema,
});

export const DetachManagedPolicyParamsSchema = z.object({
  principalId: guidSchema,
  managedPolicyId: guidSchema,
});

export const GetPrincipalPoliciesParamsSchema = z.object({
  principalId: guidSchema,
});

export const GetPrincipalsByResourceParamsSchema = z.object({
  resourceType: z.string().trim().min(1, "Resource type is required."),
  resourceId: z.string().trim().min(1, "Resource ID is required."),
});
