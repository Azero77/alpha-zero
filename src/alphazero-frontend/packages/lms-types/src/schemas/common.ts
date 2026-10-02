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
export const createPagedResultSchema = <T extends z.ZodTypeAny>(
  itemSchema: T
) =>
  z.object({
    items: z.array(itemSchema).default([]),
    totalCount: z.number().int().min(0),
    currentPage: z.number().int().min(1),
    pageSize: z.number().int().min(1),
    totalPages: z.number().int().min(0),
    hasNextPage: z.boolean().optional(),
    hasPreviousPage: z.boolean().optional(),
  });

/**
 * AlphaZero Resource Name (ARN) schema validator
 * Format: az:<service>:<tenantId>:<resource-path>
 * Example: az:courses:tenant123:course/c1/section/s1/lesson/l1
 */
export const arnSchema = z
  .string()
  .trim()
  .regex(
    /^az:[a-z0-9-]+:[a-zA-Z0-9_-]+:.+$/,
    "Invalid AlphaZero ARN format. Expected az:<service>:<tenantId>:<resource-path>"
  );

export type Arn = z.infer<typeof arnSchema>;

export interface ParsedArn {
  resourcePath: string;
  service: string;
  tenantId: string;
}

export function parseArn(arn: string): ParsedArn {
  const parts = arn.split(":");
  if (parts.length < 4 || parts[0] !== "az") {
    throw new Error(`Invalid AlphaZero ARN format: ${arn}`);
  }
  return {
    service: parts[1],
    tenantId: parts[2],
    resourcePath: parts.slice(3).join(":"),
  };
}

export function buildArn(
  service: string,
  tenantId: string,
  resourcePath: string
): string {
  return `az:${service}:${tenantId}:${resourcePath}`;
}

export function buildCourseArn(
  tenantId: string,
  courseId: string,
  sectionId?: string,
  lessonId?: string
): string {
  let path = `course/${courseId}`;
  if (sectionId) {
    path += `/section/${sectionId}`;
  }
  if (lessonId) {
    path += `/lesson/${lessonId}`;
  }
  return buildArn("courses", tenantId, path);
}

/**
 * Builds a concrete video ARN (e.g. az:video:{tenantId}:video/{videoId})
 */
export function buildVideoArn(tenantId: string, videoId: string): string {
  return buildArn("video", tenantId, `video/${videoId}`);
}

/**
 * Extracts the trailing resource GUID from an ARN resource path
 * e.g. az:video:tenant-1:video/7fa85f64-5717-4562-b3fc-2c963f66afa6 -> "7fa85f64-5717-4562-b3fc-2c963f66afa6"
 */
export function extractResourceId(arn: string): string | null {
  try {
    const parsed = parseArn(arn);
    const segments = parsed.resourcePath.split("/");
    const last = segments[segments.length - 1];
    return guidSchema.safeParse(last).success ? last : null;
  } catch {
    return null;
  }
}

/**
 * Extracts the videoId specifically from a video or coursevideo ARN
 * Supports:
 * - az:video:{tenantId}:video/{videoId}
 * - az:coursevideo:{tenantId}:course/{courseId}/video/{videoId}
 */
export function extractVideoId(arn: string): string | null {
  try {
    const parsed = parseArn(arn);
    if (parsed.service === "video" || parsed.service === "coursevideo") {
      const segments = parsed.resourcePath.split("/");
      const last = segments[segments.length - 1];
      return guidSchema.safeParse(last).success ? last : null;
    }
    return null;
  } catch {
    return null;
  }
}
