import { describe, expect, it } from "vitest";
import {
  parseArn,
  extractVideoId,
  extractResourceId,
  buildVideoArn,
  buildCourseArn,
  arnSchema,
} from "@repo/lms-types";

describe("ARN Utilities", () => {
  const sampleTenantId = "3f122add-53ef-46fb-ad9e-6ea407db191a";
  const sampleVideoId = "7fa85f64-5717-4562-b3fc-2c963f66afa6";
  const sampleCourseId = "11111111-2222-3333-4444-555555555555";

  it("should validate and parse a standard video ARN", () => {
    const arn = `az:video:${sampleTenantId}:video/${sampleVideoId}`;
    expect(arnSchema.safeParse(arn).success).toBe(true);

    const parsed = parseArn(arn);
    expect(parsed.service).toBe("video");
    expect(parsed.tenantId).toBe(sampleTenantId);
    expect(parsed.resourcePath).toBe(`video/${sampleVideoId}`);
  });

  it("should correctly extract videoId from az:video ARN", () => {
    const arn = `az:video:${sampleTenantId}:video/${sampleVideoId}`;
    const videoId = extractVideoId(arn);
    expect(videoId).toBe(sampleVideoId);
  });

  it("should correctly extract videoId from az:coursevideo ARN", () => {
    const arn = `az:coursevideo:${sampleTenantId}:course/${sampleCourseId}/video/${sampleVideoId}`;
    const videoId = extractVideoId(arn);
    expect(videoId).toBe(sampleVideoId);
  });

  it("should return null when extracting videoId from a non-video ARN", () => {
    const arn = `az:courses:${sampleTenantId}:course/${sampleCourseId}`;
    const videoId = extractVideoId(arn);
    expect(videoId).toBeNull();
  });

  it("should extract generic resourceId from trailing path", () => {
    const courseArn = `az:courses:${sampleTenantId}:course/${sampleCourseId}`;
    expect(extractResourceId(courseArn)).toBe(sampleCourseId);

    const videoArn = `az:video:${sampleTenantId}:video/${sampleVideoId}`;
    expect(extractResourceId(videoArn)).toBe(sampleVideoId);
  });

  it("should build video and course ARNs correctly", () => {
    const videoArn = buildVideoArn(sampleTenantId, sampleVideoId);
    expect(videoArn).toBe(`az:video:${sampleTenantId}:video/${sampleVideoId}`);

    const courseArn = buildCourseArn(sampleTenantId, sampleCourseId);
    expect(courseArn).toBe(`az:courses:${sampleTenantId}:course/${sampleCourseId}`);
  });

  it("should throw or return null for malformed ARNs", () => {
    expect(() => parseArn("not-an-arn")).toThrow();
    expect(extractVideoId("not-an-arn")).toBeNull();
    expect(extractResourceId("az:video:tenant:invalid-guid")).toBeNull();
  });
});
