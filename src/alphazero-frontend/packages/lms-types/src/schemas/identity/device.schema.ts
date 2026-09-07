import { z } from "zod";
import { guidSchema } from "../common";

export const RegisterDeviceRequestSchema = z.object({
  deviceIdentifier: z.string().trim().min(1, "Device identifier is required."),
  deviceName: z
    .string()
    .trim()
    .min(1, "Device name is required.")
    .max(100, "Device name cannot exceed 100 characters."),
  deviceType: z.string().trim().min(1, "Device type is required."),
  os: z.string().trim().optional(),
  browser: z.string().trim().optional(),
});
export type RegisterDeviceRequest = z.infer<typeof RegisterDeviceRequestSchema>;

export const RegisterDeviceResponseSchema = z.object({
  deviceId: guidSchema,
});
export type RegisterDeviceResponse = z.infer<typeof RegisterDeviceResponseSchema>;

export const SetMainDeviceRequestSchema = z.object({
  deviceId: guidSchema,
});
export type SetMainDeviceRequest = z.infer<typeof SetMainDeviceRequestSchema>;
