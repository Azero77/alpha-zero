import { z } from "zod";
import { guidSchema } from "../common";

export const DeviceFingerprintDtoSchema = z.object({
  deviceId: z.string().trim().min(1, "Device ID is required."),
  deviceName: z.string().trim().max(100).optional(),
  deviceType: z.string().trim().optional(),
  os: z.string().trim().optional(),
  browser: z.string().trim().optional(),
});
export type DeviceFingerprintDto = z.infer<typeof DeviceFingerprintDtoSchema>;

export const RegisterStudentRequestSchema = z.object({
  phoneNumber: z
    .string()
    .trim()
    .regex(/^\+?[1-9]\d{7,14}$/, "Invalid phone number format."),
  password: z.string().min(8, "Password must be at least 8 characters."),
  fullName: z
    .string()
    .trim()
    .min(1, "Full name is required.")
    .max(100, "Full name cannot exceed 100 characters."),
  deviceFingerprint: DeviceFingerprintDtoSchema,
});
export type RegisterStudentRequest = z.infer<typeof RegisterStudentRequestSchema>;

export const AuthResultResponseSchema = z.object({
  token: z.string(),
  refreshToken: z.string().optional(),
  userId: guidSchema,
});
export type AuthResultResponse = z.infer<typeof AuthResultResponseSchema>;

export const LoginPrincipalRequestSchema = z.object({
  email: z.string().trim().email("Invalid email address format."),
  password: z.string().min(1, "Password is required."),
});
export type LoginPrincipalRequest = z.infer<typeof LoginPrincipalRequestSchema>;

export const PrincipalAuthResultResponseSchema = z.object({
  token: z.string(),
  principalId: guidSchema,
});
export type PrincipalAuthResultResponse = z.infer<typeof PrincipalAuthResultResponseSchema>;

export const LoginAsTenantUserRequestSchema = z.object({
  tenantId: guidSchema,
  deviceFingerprint: DeviceFingerprintDtoSchema.nullable().optional(),
});
export type LoginAsTenantUserRequest = z.infer<typeof LoginAsTenantUserRequestSchema>;

export const TenantAuthResultResponseSchema = z.object({
  token: z.string(),
  tenantId: guidSchema,
});
export type TenantAuthResultResponse = z.infer<typeof TenantAuthResultResponseSchema>;
