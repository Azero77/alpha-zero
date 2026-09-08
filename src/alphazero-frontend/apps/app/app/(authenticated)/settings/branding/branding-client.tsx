"use client";

import { useState, useTransition, useMemo } from "react";
import { toast } from "@repo/design-system/components/ui/sonner";
import {
  DEFAULT_PRIMARY_COLOR,
  getContrastRatio,
  getContrastTextColor,
  getLuminance,
  generateTenantCssVariables,
  TEXT_DARK,
  TEXT_LIGHT,
} from "@repo/design-system/lib/color-utils";
import type { TenantLookupResponse } from "@repo/design-system/lib/tenant-utils";
import { updateTenantBrandingAction } from "@/app/actions/tenants/update-branding";
import { Button } from "@repo/design-system/components/ui/button";
import { Input } from "@repo/design-system/components/ui/input";
import { Label } from "@repo/design-system/components/ui/label";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@repo/design-system/components/ui/card";
import { Badge } from "@repo/design-system/components/ui/badge";
import { Switch } from "@repo/design-system/components/ui/switch";
import {
  CheckCircle2Icon,
  RotateCcwIcon,
  SaveIcon,
  SunIcon,
  MoonIcon,
  SparklesIcon,
  ShieldCheckIcon,
  AlertTriangleIcon,
  BookOpenIcon,
  KeyRoundIcon,
  DownloadIcon,
  CheckIcon,
} from "lucide-react";

interface BrandingClientProps {
  initialTenant: {
    id: string;
    name: string;
    subdomain: string;
    primaryColor: string;
    secondaryColor?: string | null;
    logoUrl?: string | null;
    darkModeLogoUrl?: string | null;
    faviconUrl?: string | null;
  };
}

const PRESET_PRIMARY_COLORS = [
  { name: "Precision Blue (Default)", hex: "#2563eb" },
  { name: "Emerald Forest", hex: "#059669" },
  { name: "Deep Navy", hex: "#1e3a8a" },
  { name: "Crimson Academy", hex: "#a51c30" },
  { name: "Sunset Amber", hex: "#d97706" },
  { name: "Obsidian Slate", hex: "#18181b" },
];

const PRESET_SECONDARY_COLORS = [
  { name: "Gold Amber", hex: "#f59e0b" },
  { name: "Tangerine Coral", hex: "#f97316" },
  { name: "Signal Teal", hex: "#0284c7" },
  { name: "Iris Violet", hex: "#6366f1" },
  { name: "Rose Quartz", hex: "#e11d48" },
];

export function BrandingClient({ initialTenant }: BrandingClientProps) {
  const [name, setName] = useState(initialTenant.name);
  const [primaryColor, setPrimaryColor] = useState(initialTenant.primaryColor || DEFAULT_PRIMARY_COLOR);
  const [enableSecondary, setEnableSecondary] = useState(Boolean(initialTenant.secondaryColor));
  const [secondaryColor, setSecondaryColor] = useState(initialTenant.secondaryColor || "#f59e0b");
  const [logoUrl, setLogoUrl] = useState(initialTenant.logoUrl || "");
  const [darkModeLogoUrl, setDarkModeLogoUrl] = useState(initialTenant.darkModeLogoUrl || "");
  const [faviconUrl, setFaviconUrl] = useState(initialTenant.faviconUrl || "");
  const [previewMode, setPreviewMode] = useState<"dark" | "light">("dark");
  const [isPending, startTransition] = useTransition();

  // Validate hex format
  const isPrimaryValidHex = useMemo(
    () => /^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/.test(primaryColor.trim()),
    [primaryColor]
  );
  const isSecondaryValidHex = useMemo(
    () => /^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/.test(secondaryColor.trim()),
    [secondaryColor]
  );

  // Derived WCAG contrast values
  const safePrimary = isPrimaryValidHex ? primaryColor.trim() : DEFAULT_PRIMARY_COLOR;
  const safeSecondary = enableSecondary && isSecondaryValidHex ? secondaryColor.trim() : null;

  const primaryFg = useMemo(() => getContrastTextColor(safePrimary), [safePrimary]);
  const primaryLum = useMemo(() => getLuminance(safePrimary), [safePrimary]);
  const contrastRatio = useMemo(() => getContrastRatio(safePrimary, primaryFg), [safePrimary, primaryFg]);

  const wcagStatus = useMemo(() => {
    if (contrastRatio >= 7.0) {
      return { label: `WCAG AAA Pass (${contrastRatio.toFixed(1)}:1)`, variant: "default" as const, passed: true };
    }
    if (contrastRatio >= 4.5) {
      return { label: `WCAG AA Pass (${contrastRatio.toFixed(1)}:1)`, variant: "default" as const, passed: true };
    }
    return { label: `Low Contrast (${contrastRatio.toFixed(1)}:1)`, variant: "destructive" as const, passed: false };
  }, [contrastRatio]);

  // Dynamic CSS styles for the sandbox container
  const sandboxStyle = useMemo(() => {
    const css = generateTenantCssVariables(safePrimary, safeSecondary);
    return {
      "--primary": safePrimary,
      "--primary-foreground": primaryFg,
      "--color-precision-blue": safePrimary,
      "--ring": `${safePrimary}80`,
      ...(safeSecondary
        ? {
            "--secondary": safeSecondary,
            "--secondary-foreground": getContrastTextColor(safeSecondary),
          }
        : {}),
    } as React.CSSProperties;
  }, [safePrimary, safeSecondary, primaryFg]);

  const handleResetDefaults = () => {
    setPrimaryColor(DEFAULT_PRIMARY_COLOR);
    setEnableSecondary(false);
    setSecondaryColor("#f59e0b");
    toast.info("Reset colors to AlphaZero default (Precision Blue #2563eb).");
  };

  const handleSave = () => {
    if (!isPrimaryValidHex) {
      toast.error("Please enter a valid 6-character hex for Primary Brand Color (e.g. #2563eb).");
      return;
    }
    if (enableSecondary && !isSecondaryValidHex) {
      toast.error("Please enter a valid 6-character hex for Secondary Accent Color.");
      return;
    }

    startTransition(async () => {
      const result = await updateTenantBrandingAction({
        tenantId: initialTenant.id,
        name,
        primaryColor: safePrimary,
        secondaryColor: enableSecondary ? safeSecondary : null,
        logoUrl: logoUrl.trim() || null,
        darkModeLogoUrl: darkModeLogoUrl.trim() || null,
        faviconUrl: faviconUrl.trim() || null,
      });

      if (result.error) {
        toast.error(result.error);
        return;
      }

      // Dynamically update the root style tag in the current document for instant live preview
      const dynamicCss = generateTenantCssVariables(safePrimary, enableSecondary ? safeSecondary : null);
      let styleTag = document.getElementById("tenant-theme");
      if (!styleTag) {
        styleTag = document.createElement("style");
        styleTag.id = "tenant-theme";
        document.head.appendChild(styleTag);
      }
      styleTag.innerHTML = dynamicCss;

      toast.success("Academy branding updated successfully! Edge and browser cache purged.");
    });
  };

  return (
    <div className="grid grid-cols-1 gap-8 lg:grid-cols-12">
      {/* ─── LEFT COLUMN: Brand Configuration Form ─────────────────── */}
      <div className="flex flex-col gap-6 lg:col-span-6">
        <Card className="border-border bg-card">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="text-xl font-semibold tracking-tight">
                  Dynamic Branding & Identity
                </CardTitle>
                <CardDescription className="text-sm text-muted-foreground">
                  Customize your academy's brand colors, logos, and visual theme.
                </CardDescription>
              </div>
              <Badge variant="outline" className="font-mono text-xs">
                {initialTenant.subdomain}.alphazero.academy
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-6">
            {/* Academy Name */}
            <div className="space-y-2">
              <Label htmlFor="academyName">Academy Name</Label>
              <Input
                id="academyName"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. Damascus Institute of Technology"
              />
            </div>

            {/* Primary Brand Color */}
            <div className="space-y-3 rounded-lg border border-border p-4 bg-muted/20">
              <div className="flex items-center justify-between">
                <div>
                  <Label className="text-sm font-semibold">Primary Brand Color</Label>
                  <p className="text-xs text-muted-foreground">
                    Drives primary action buttons, syllabus progress tracks, and active indicators.
                  </p>
                </div>
                <div
                  className="size-8 rounded-md border border-border shadow-xs"
                  style={{ backgroundColor: safePrimary }}
                />
              </div>

              <div className="flex items-center gap-3">
                <input
                  type="color"
                  value={safePrimary}
                  onChange={(e) => setPrimaryColor(e.target.value)}
                  className="size-10 cursor-pointer rounded border border-border bg-transparent p-0.5"
                  title="Choose primary color"
                />
                <Input
                  value={primaryColor}
                  onChange={(e) => setPrimaryColor(e.target.value)}
                  placeholder="#2563eb"
                  className={`font-mono uppercase ${!isPrimaryValidHex ? "border-destructive focus-visible:ring-destructive" : ""}`}
                />
              </div>

              {/* Quick Presets */}
              <div className="space-y-1.5 pt-1">
                <span className="text-xs text-muted-foreground font-medium">Curated Presets:</span>
                <div className="flex flex-wrap gap-2">
                  {PRESET_PRIMARY_COLORS.map((preset) => (
                    <button
                      key={preset.hex}
                      type="button"
                      onClick={() => setPrimaryColor(preset.hex)}
                      className={`group flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs transition-all ${
                        safePrimary.toLowerCase() === preset.hex.toLowerCase()
                          ? "border-primary font-semibold bg-primary/10 text-foreground"
                          : "border-border hover:bg-muted text-muted-foreground"
                      }`}
                    >
                      <span className="size-2.5 rounded-full" style={{ backgroundColor: preset.hex }} />
                      <span>{preset.name}</span>
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Secondary Accent Color */}
            <div className="space-y-3 rounded-lg border border-border p-4 bg-muted/20">
              <div className="flex items-center justify-between">
                <div>
                  <Label className="text-sm font-semibold">Secondary Accent Color</Label>
                  <p className="text-xs text-muted-foreground">
                    Optional accent for category pills, secondary CTAs, and highlighted badges.
                  </p>
                </div>
                <Switch
                  checked={enableSecondary}
                  onCheckedChange={setEnableSecondary}
                  aria-label="Enable secondary accent color"
                />
              </div>

              {enableSecondary && (
                <div className="space-y-3 pt-2">
                  <div className="flex items-center gap-3">
                    <input
                      type="color"
                      value={safeSecondary || "#f59e0b"}
                      onChange={(e) => setSecondaryColor(e.target.value)}
                      className="size-10 cursor-pointer rounded border border-border bg-transparent p-0.5"
                      title="Choose secondary color"
                    />
                    <Input
                      value={secondaryColor}
                      onChange={(e) => setSecondaryColor(e.target.value)}
                      placeholder="#f59e0b"
                      className={`font-mono uppercase ${!isSecondaryValidHex ? "border-destructive focus-visible:ring-destructive" : ""}`}
                    />
                  </div>

                  <div className="flex flex-wrap gap-2">
                    {PRESET_SECONDARY_COLORS.map((preset) => (
                      <button
                        key={preset.hex}
                        type="button"
                        onClick={() => setSecondaryColor(preset.hex)}
                        className={`flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs transition-all ${
                          secondaryColor.toLowerCase() === preset.hex.toLowerCase()
                            ? "border-primary font-semibold bg-primary/10 text-foreground"
                            : "border-border hover:bg-muted text-muted-foreground"
                        }`}
                      >
                        <span className="size-2.5 rounded-full" style={{ backgroundColor: preset.hex }} />
                        <span>{preset.name}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>

            {/* Logos & Assets */}
            <div className="space-y-4">
              <Label className="text-sm font-semibold">Brand Assets & Logos</Label>
              <div className="space-y-3">
                <div className="space-y-1.5">
                  <Label htmlFor="logoUrl" className="text-xs text-muted-foreground">
                    Primary Logo URL (Light Mode)
                  </Label>
                  <Input
                    id="logoUrl"
                    value={logoUrl}
                    onChange={(e) => setLogoUrl(e.target.value)}
                    placeholder="https://cdn.example.com/logo-light.svg"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="darkLogoUrl" className="text-xs text-muted-foreground">
                    Dark Mode Logo URL (Optional)
                  </Label>
                  <Input
                    id="darkLogoUrl"
                    value={darkModeLogoUrl}
                    onChange={(e) => setDarkModeLogoUrl(e.target.value)}
                    placeholder="https://cdn.example.com/logo-dark.svg"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="faviconUrl" className="text-xs text-muted-foreground">
                    Favicon URL (Optional)
                  </Label>
                  <Input
                    id="faviconUrl"
                    value={faviconUrl}
                    onChange={(e) => setFaviconUrl(e.target.value)}
                    placeholder="https://cdn.example.com/favicon.ico"
                  />
                </div>
              </div>
            </div>
          </CardContent>
          <CardFooter className="flex items-center justify-between border-t border-border p-4">
            <Button
              variant="outline"
              size="sm"
              onClick={handleResetDefaults}
              disabled={isPending}
              className="gap-1.5"
            >
              <RotateCcwIcon className="size-3.5" />
              Reset to Default
            </Button>
            <Button
              size="sm"
              onClick={handleSave}
              disabled={isPending || !isPrimaryValidHex}
              className="gap-1.5"
            >
              <SaveIcon className="size-3.5" />
              {isPending ? "Saving..." : "Save Branding"}
            </Button>
          </CardFooter>
        </Card>
      </div>

      {/* ─── RIGHT COLUMN: Real-Time Interactive Live Sandbox ────────── */}
      <div className="flex flex-col gap-6 lg:col-span-6">
        <Card className="border-border bg-card">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="text-lg font-semibold flex items-center gap-2">
                  <SparklesIcon className="size-4 text-primary" />
                  Live Component Sandbox
                </CardTitle>
                <CardDescription className="text-xs text-muted-foreground">
                  Interactive real-time preview showing your dynamic brand styles.
                </CardDescription>
              </div>

              {/* Theme Mode Preview Toggle */}
              <div className="flex items-center gap-1 rounded-lg border border-border p-1 bg-muted/30">
                <Button
                  variant={previewMode === "dark" ? "secondary" : "ghost"}
                  size="icon-sm"
                  onClick={() => setPreviewMode("dark")}
                  title="Dark Command Center Mode"
                >
                  <MoonIcon className="size-3.5" />
                </Button>
                <Button
                  variant={previewMode === "light" ? "secondary" : "ghost"}
                  size="icon-sm"
                  onClick={() => setPreviewMode("light")}
                  title="Light Study Mode"
                >
                  <SunIcon className="size-3.5" />
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent className="space-y-6">
            {/* Live WCAG Contrast Indicator */}
            <div className="flex flex-col gap-3 rounded-lg border border-border p-3.5 bg-muted/10">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <ShieldCheckIcon className="size-4 text-green-500" />
                  <span className="text-xs font-semibold uppercase tracking-wider">
                    WCAG 2.1 Contrast Guarantee
                  </span>
                </div>
                <Badge variant={wcagStatus.variant} className="text-xs font-mono">
                  {wcagStatus.label}
                </Badge>
              </div>

              <div className="grid grid-cols-2 gap-2 text-xs">
                <div className="flex items-center justify-between rounded bg-muted/40 p-2">
                  <span className="text-muted-foreground">Relative Luminance:</span>
                  <span className="font-mono font-medium">{primaryLum.toFixed(3)}</span>
                </div>
                <div className="flex items-center justify-between rounded bg-muted/40 p-2">
                  <span className="text-muted-foreground">Computed Text:</span>
                  <div className="flex items-center gap-1.5">
                    <span
                      className="size-3 rounded-full border"
                      style={{ backgroundColor: primaryFg }}
                    />
                    <span className="font-mono font-medium">
                      {primaryFg === TEXT_DARK ? "Dark (#09090b)" : "Light (#ffffff)"}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            {/* Themed Preview Canvas */}
            <div
              className={`rounded-xl border border-border p-6 transition-colors duration-200 ${
                previewMode === "dark" ? "dark bg-[#09090b] text-[#f4f4f5]" : "bg-[#ffffff] text-[#09090b]"
              }`}
              style={sandboxStyle}
            >
              <div className="space-y-6">
                {/* Academy Header Preview */}
                <div className="flex items-center justify-between border-b border-border/60 pb-3">
                  <div className="flex items-center gap-2.5">
                    <div
                      className="size-7 rounded-md flex items-center justify-center font-bold text-xs shadow-xs"
                      style={{ backgroundColor: safePrimary, color: primaryFg }}
                    >
                      {name.slice(0, 2).toUpperCase() || "AZ"}
                    </div>
                    <span className="text-sm font-semibold tracking-tight">{name || "AlphaZero Academy"}</span>
                  </div>
                  <Badge variant="outline" className="text-[10px] font-mono border-border">
                    MENA Low-Bandwidth Optimized
                  </Badge>
                </div>

                {/* Course Card Preview */}
                <div className="rounded-xl border border-border bg-card/60 p-4 shadow-sm space-y-4">
                  <div className="flex items-start justify-between gap-4">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <Badge
                          variant="secondary"
                          className="text-[10px] font-medium"
                          style={
                            safeSecondary
                              ? { backgroundColor: safeSecondary, color: getContrastTextColor(safeSecondary) }
                              : {}
                          }
                        >
                          Computer Science
                        </Badge>
                        <span className="text-xs text-muted-foreground font-mono">CS-504</span>
                      </div>
                      <h4 className="text-sm font-semibold tracking-tight">
                        Enterprise Clean Architecture with .NET 9 & Next.js 15
                      </h4>
                      <p className="text-xs text-muted-foreground">
                        Instructor: Dr. Tariq Al-Khatib · 24 Modular Lessons
                      </p>
                    </div>
                  </div>

                  {/* Bitmask Progress Bar */}
                  <div className="space-y-1.5 pt-1">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-muted-foreground flex items-center gap-1.5">
                        <CheckCircle2Icon className="size-3.5" style={{ color: safePrimary }} />
                        Bitmask Progress Tracking
                      </span>
                      <span className="font-mono text-[11px] font-medium" style={{ color: safePrimary }}>
                        18/24 Lessons (75%)
                      </span>
                    </div>
                    {/* Custom progress track with primary color fill */}
                    <div className="h-2 w-full overflow-hidden rounded-full bg-muted">
                      <div
                        className="h-full rounded-full transition-all duration-300"
                        style={{ width: "75%", backgroundColor: safePrimary }}
                      />
                    </div>
                  </div>

                  {/* CTA Buttons Preview */}
                  <div className="flex flex-wrap items-center gap-2.5 pt-2">
                    <button
                      type="button"
                      className="inline-flex h-8 items-center justify-center gap-1.5 rounded-md px-3.5 text-xs font-medium shadow-xs transition-opacity hover:opacity-90 active:scale-98"
                      style={{ backgroundColor: safePrimary, color: primaryFg }}
                    >
                      <BookOpenIcon className="size-3.5" />
                      Enroll in Course
                    </button>
                    <button
                      type="button"
                      className="inline-flex h-8 items-center justify-center gap-1.5 rounded-md border border-border px-3 text-xs font-medium hover:bg-muted/50 transition-colors"
                    >
                      <DownloadIcon className="size-3.5" />
                      Preview Syllabus
                    </button>
                  </div>
                </div>

                {/* Physical Library Voucher Card Preview */}
                <div className="rounded-xl border border-dashed border-border/80 p-4 bg-muted/20 space-y-3">
                  <div className="flex items-center gap-2 text-xs font-semibold">
                    <KeyRoundIcon className="size-3.5" style={{ color: safePrimary }} />
                    Syrian/MENA Physical Voucher Economy
                  </div>
                  <p className="text-[11px] text-muted-foreground">
                    Students unlock premium content with physical scratch-off library cards.
                  </p>
                  <div className="flex items-center gap-2">
                    <div className="flex-1 rounded-md border border-border bg-background px-3 py-1.5 font-mono text-xs tracking-wider text-center">
                      AZ-9482-K92X
                    </div>
                    <button
                      type="button"
                      className="inline-flex h-8 items-center justify-center gap-1 rounded-md px-3 text-xs font-medium transition-opacity hover:opacity-90"
                      style={{ backgroundColor: safePrimary, color: primaryFg }}
                    >
                      <CheckIcon className="size-3.5" />
                      Redeem
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
