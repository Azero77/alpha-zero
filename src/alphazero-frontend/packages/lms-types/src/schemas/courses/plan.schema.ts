import { z } from "zod";
import { guidSchema } from "../common";

export const PlanTypeEnum = z.enum(["OneTime", "Subscription", "CustomTimeLimit"], {
  message: "Plan type must be OneTime, Subscription, or CustomTimeLimit.",
});
export type PlanType = z.infer<typeof PlanTypeEnum>;

export const AddPlanParamsSchema = z.object({
  courseId: guidSchema,
});

export const AddPlanRequestSchema = z
  .object({
    name: z
      .string()
      .trim()
      .min(1, "Plan name is required.")
      .max(100, "Plan name cannot exceed 100 characters."),
    price: z.number().min(0, "Price cannot be negative."),
    currency: z
      .string()
      .trim()
      .length(3, "Currency must be a 3-character ISO code.")
      .toUpperCase(),
    type: PlanTypeEnum,
    durationDays: z.number().int().positive("Duration in days must be positive.").nullable().optional(),
    libraryCodeMaxUsage: z
      .number()
      .int()
      .positive("Max usage must be positive.")
      .nullable()
      .optional(),
  })
  .refine(
    (data) => {
      if (data.type === "Subscription" || data.type === "CustomTimeLimit") {
        return typeof data.durationDays === "number" && data.durationDays > 0;
      }
      return true;
    },
    {
      message: "DurationDays is required and must be positive for Subscription and CustomTimeLimit plans.",
      path: ["durationDays"],
    }
  );
export type AddPlanRequest = z.infer<typeof AddPlanRequestSchema>;

export const AddPlanResponseSchema = z.object({
  planId: guidSchema,
});
export type AddPlanResponse = z.infer<typeof AddPlanResponseSchema>;

export const UpdatePlanParamsSchema = z.object({
  courseId: guidSchema,
  planId: guidSchema,
});

export const UpdatePlanRequestSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Plan name is required.")
    .max(100, "Plan name cannot exceed 100 characters."),
  price: z.number().min(0, "Price cannot be negative."),
  currency: z
    .string()
    .trim()
    .length(3, "Currency must be a 3-character ISO code.")
    .toUpperCase(),
  type: PlanTypeEnum,
  durationDays: z.number().int().positive().nullable().optional(),
  libraryCodeMaxUsage: z.number().int().positive().nullable().optional(),
  isActive: z.boolean(),
});
export type UpdatePlanRequest = z.infer<typeof UpdatePlanRequestSchema>;

export const RemovePlanParamsSchema = z.object({
  courseId: guidSchema,
  planId: guidSchema,
});
