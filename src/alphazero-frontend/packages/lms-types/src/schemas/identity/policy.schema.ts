import { z } from "zod";
import { guidSchema } from "../common";

export const PolicyStatementDtoSchema = z.object({
  effect: z.enum(["Allow", "Deny"], { message: "Effect must be Allow or Deny." }),
  action: z.string().trim().min(1, "Action is required."),
  resource: z.string().trim().min(1, "Resource ARN pattern is required."),
  condition: z.record(z.string(), z.unknown()).nullable().optional(),
});
export type PolicyStatementDto = z.infer<typeof PolicyStatementDtoSchema>;

export const CreateManagedPolicyRequestSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Policy name is required.")
    .max(100, "Policy name cannot exceed 100 characters."),
  description: z.string().max(500).nullable().optional(),
  statements: z.array(PolicyStatementDtoSchema).min(1, "At least one statement is required."),
});
export type CreateManagedPolicyRequest = z.infer<typeof CreateManagedPolicyRequestSchema>;

export const CreatePolicyResponseSchema = z.object({
  policyId: guidSchema,
});
export type CreatePolicyResponse = z.infer<typeof CreatePolicyResponseSchema>;

export const DeleteManagedPolicyParamsSchema = z.object({
  policyId: guidSchema,
});
export type DeleteManagedPolicyParams = z.infer<typeof DeleteManagedPolicyParamsSchema>;
