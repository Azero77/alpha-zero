import { z } from "zod";
import type { components } from "./api";

/**
 * Matches C# AlphaZero.Shared.Presentation.Extensions.ErrorDto
 * public sealed record ErrorDto(string Code, string Description, string Type, object? Metadata)
 */
export const ErrorDtoSchema = z.object({
  code: z.string(),
  description: z.string(),
  type: z.enum([
    "Failure",
    "Unexpected",
    "Validation",
    "Conflict",
    "NotFound",
    "Unauthorized",
    "Forbidden",
  ]),
  metadata: z.record(z.string(), z.unknown()).nullable().optional(),
});

export type ErrorDto = z.infer<typeof ErrorDtoSchema>;

/**
 * Matches C# AlphaZero.Shared.Presentation.Extensions.UnifiedErrorBody
 * and is 100% compatible with components["schemas"]["ProblemDetails"] in api.d.ts.
 */
export const ApiErrorResponseSchema = z.object({
  status: z.number().int(),
  title: z.string(),
  type: z.string().nullable().optional(),
  detail: z.string().nullable().optional(),
  instance: z.string().nullable().optional(),
  traceId: z.string().nullable().optional(),
  errors: z.array(ErrorDtoSchema).default([]),
});

/**
 * Refined ProblemDetails type that satisfies OpenAPI's ProblemDetails
 * while providing strongly-typed access to `errors: ErrorDto[]`.
 */
export type ApiErrorResponse = z.infer<typeof ApiErrorResponseSchema> &
  components["schemas"]["ProblemDetails"];

/**
 * Typed Exception thrown by the API client when an error response is received.
 */
export class ApiErrorException extends Error {
  readonly response: ApiErrorResponse;

  constructor(response: ApiErrorResponse) {
    const primaryMessage =
      response.errors[0]?.description ||
      response.detail ||
      response.title ||
      `API Error (${response.status})`;
    super(primaryMessage);
    this.name = "ApiErrorException";
    this.response = response;
  }

  /**
   * Helper to extract field-level validation errors for React Hook Form.
   * Reads propertyName from ErrorDto metadata (set by FluentValidation in C#)
   * or falls back to the error code prefix.
   */
  getFieldErrors(): Record<string, string> {
    const fieldErrors: Record<string, string> = {};

    for (const err of this.response.errors) {
      if (err.type === "Validation") {
        const metadataProp = (err.metadata as Record<string, unknown> | null)
          ?.propertyName;
        const fieldName =
          typeof metadataProp === "string"
            ? metadataProp
            : err.code.split(".")[0];

        const normalizedField = fieldName?.toLowerCase();

        if (normalizedField && !fieldErrors[normalizedField]) {
          fieldErrors[normalizedField] = err.description;
        }
      }
    }

    return fieldErrors;
  }

  /**
   * Helper to get the primary domain error code for bilingual dictionary lookup.
   * e.g. "Voucher.Expired", "Course.NotFound", "Validation.Failed"
   */
  getPrimaryCode(): string {
    return (
      this.response.errors[0]?.code || this.response.title || "UNKNOWN_ERROR"
    );
  }

  /**
   * Helper to check if this error represents an authorization / IAM denial.
   */
  isForbidden(): boolean {
    return (
      this.response.status === 403 ||
      this.response.errors.some((e) => e.type === "Forbidden")
    );
  }

  /**
   * Helper to check if this error is an authentication failure.
   */
  isUnauthorized(): boolean {
    return (
      this.response.status === 401 ||
      this.response.errors.some((e) => e.type === "Unauthorized")
    );
  }

  /**
   * Helper to extract traceId from ASP.NET Core for support / Sentry.
   */
  getTraceId(): string | null | undefined {
    return this.response.traceId;
  }
}
