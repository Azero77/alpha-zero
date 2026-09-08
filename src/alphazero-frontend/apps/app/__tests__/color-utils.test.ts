import { describe, expect, it } from "vitest";
import {
  adjustBrightness,
  generateTenantCssVariables,
  getContrastRatio,
  getContrastTextColor,
  getLuminance,
  hexToRgb,
  TEXT_DARK,
  TEXT_LIGHT,
} from "@repo/design-system/lib/color-utils";

describe("color-utils", () => {
  describe("hexToRgb", () => {
    it("parses standard 6-character hex", () => {
      expect(hexToRgb("#2563eb")).toEqual({ r: 37, g: 99, b: 235 });
      expect(hexToRgb("#ffffff")).toEqual({ r: 255, g: 255, b: 255 });
      expect(hexToRgb("#000000")).toEqual({ r: 0, g: 0, b: 0 });
    });

    it("parses 3-character hex shorthand", () => {
      expect(hexToRgb("#fff")).toEqual({ r: 255, g: 255, b: 255 });
      expect(hexToRgb("#000")).toEqual({ r: 0, g: 0, b: 0 });
    });

    it("falls back gracefully on malformed hex", () => {
      expect(hexToRgb("invalid")).toEqual({ r: 37, g: 99, b: 235 });
    });
  });

  describe("getLuminance", () => {
    it("calculates relative luminance correctly for standard colors", () => {
      expect(getLuminance("#ffffff")).toBeCloseTo(1.0, 2);
      expect(getLuminance("#000000")).toBeCloseTo(0.0, 2);
      // Precision Blue (#2563eb) relative luminance is approx ~0.16
      expect(getLuminance("#2563eb")).toBeGreaterThan(0.1);
      expect(getLuminance("#2563eb")).toBeLessThan(0.3);
    });
  });

  describe("getContrastRatio", () => {
    it("computes maximum contrast (21:1) between black and white", () => {
      const ratio = getContrastRatio("#ffffff", "#000000");
      expect(ratio).toBeCloseTo(21.0, 1);
    });

    it("computes 1:1 contrast for identical colors", () => {
      const ratio = getContrastRatio("#2563eb", "#2563eb");
      expect(ratio).toBeCloseTo(1.0, 1);
    });
  });

  describe("WCAG 2.1 Contrast Text Decision (Acceptance Criteria)", () => {
    it("Pure White (#FFFFFF) -> Primary Foreground must be #09090b", () => {
      expect(getContrastTextColor("#FFFFFF")).toBe(TEXT_DARK);
    });

    it("Neon Yellow (#FFFF00) -> Primary Foreground must be #09090b", () => {
      expect(getContrastTextColor("#FFFF00")).toBe(TEXT_DARK);
    });

    it("Deep Navy (#0F172A) -> Primary Foreground must be #ffffff", () => {
      expect(getContrastTextColor("#0F172A")).toBe(TEXT_LIGHT);
    });

    it("Crimson (#A51C30) -> Primary Foreground must be #ffffff", () => {
      expect(getContrastTextColor("#A51C30")).toBe(TEXT_LIGHT);
    });

    it("AlphaZero Precision Blue (#2563eb) -> Primary Foreground must be #ffffff", () => {
      expect(getContrastTextColor("#2563eb")).toBe(TEXT_LIGHT);
    });
  });

  describe("generateTenantCssVariables", () => {
    it("generates CSS with default primary color when no arguments provided", () => {
      const css = generateTenantCssVariables();
      expect(css).toContain("--primary: #2563eb");
      expect(css).toContain(`--primary-foreground: ${TEXT_LIGHT}`);
      expect(css).toContain("--ring: rgba(37, 99, 235, 0.5)");
      expect(css).toContain(":root");
      expect(css).toContain(".dark");
    });

    it("generates CSS with custom primary and secondary brand colors", () => {
      const css = generateTenantCssVariables("#FFFF00", "#0F172A");
      expect(css).toContain("--primary: #FFFF00");
      expect(css).toContain(`--primary-foreground: ${TEXT_DARK}`);
      expect(css).toContain("--secondary: #0F172A");
      expect(css).toContain(`--secondary-foreground: ${TEXT_LIGHT}`);
    });

    it("produces compact output around ~200-400 bytes", () => {
      const css = generateTenantCssVariables("#1E3A8A", "#F59E0B");
      const byteLength = new TextEncoder().encode(css).length;
      expect(byteLength).toBeLessThan(600);
      expect(byteLength).toBeGreaterThan(100);
    });
  });
});
