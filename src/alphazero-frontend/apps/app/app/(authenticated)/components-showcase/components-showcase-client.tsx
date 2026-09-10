"use client";

import * as React from "react";
import {
  BitmaskProgressBar,
  VoucherInput,
  CourseCard,
} from "@repo/design-system";
import { Button } from "@repo/design-system/components/ui/button";
import { Input } from "@repo/design-system/components/ui/input";
import { Label } from "@repo/design-system/components/ui/label";
import { Badge } from "@repo/design-system/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/design-system/components/ui/card";
import { toast } from "@repo/design-system/components/ui/sonner";
import {
  CheckCircle2Icon,
  CopyIcon,
  GlobeIcon,
  KeyRoundIcon,
  LayersIcon,
  PlayCircleIcon,
  RefreshCwIcon,
  SlidersIcon,
  SparklesIcon,
} from "lucide-react";

export function ComponentsShowcaseClient() {
  // Global Bilingual Direction
  const [direction, setDirection] = React.useState<"ltr" | "rtl">("ltr");

  // Bitmask State
  const [bitmask, setBitmask] = React.useState("11110010");
  const [bitmaskVariant, setBitmaskVariant] = React.useState<"continuous" | "segmented">("continuous");
  const [bitmaskSize, setBitmaskSize] = React.useState<"sm" | "md" | "lg">("md");

  // Voucher State
  const [voucherCode, setVoucherCode] = React.useState("");
  const [voucherState, setVoucherState] = React.useState<"idle" | "valid" | "error" | "loading">("idle");
  const [voucherError, setVoucherError] = React.useState("This voucher code has already been redeemed or is invalid.");
  const [voucherSuccess, setVoucherSuccess] = React.useState("Voucher verified! Course access unlocked.");
  const [lastRedeemedCode, setLastRedeemedCode] = React.useState<string | null>(null);

  // Course Card State
  const [cardLayout, setCardLayout] = React.useState<"vertical" | "horizontal">("vertical");
  const [isEnrolled, setIsEnrolled] = React.useState(true);
  const [courseLanguage, setCourseLanguage] = React.useState<"en" | "ar">("en");

  // Quick helper to toggle a bit in the bitmask
  const toggleBit = (index: number) => {
    const chars = bitmask.split("");
    while (chars.length <= index) {
      chars.push("0");
    }
    chars[index] = chars[index] === "1" ? "0" : "1";
    setBitmask(chars.join(""));
  };

  const handleVoucherComplete = (fullCode: string) => {
    setLastRedeemedCode(fullCode);
    toast.success(`Voucher code complete: ${fullCode}`);
    if (voucherState === "idle") {
      setVoucherState("valid");
    }
  };

  const isRtl = direction === "rtl";

  return (
    <div
      dir={direction}
      className="space-y-8 p-4 md:p-8 max-w-6xl mx-auto transition-all"
    >
      {/* Top Banner & Direction Switcher */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 p-5 rounded-2xl border border-border/80 bg-card/60 backdrop-blur-sm shadow-xs">
        <div className="space-y-1 text-start">
          <div className="flex items-center gap-2">
            <span className="p-1.5 rounded-lg bg-primary/15 text-primary">
              <SparklesIcon className="size-4" />
            </span>
            <h1 className="text-xl font-bold tracking-tight text-foreground">
              {isRtl ? "مختبر مكونات AlphaZero LMS" : "AlphaZero LMS Component Laboratory"}
            </h1>
            <Badge variant="outline" className="font-mono text-xs">Step 3 Complete</Badge>
          </div>
          <p className="text-xs text-muted-foreground">
            {isRtl
              ? "معاينة تفاعلية حية لمكونات المنصة مع دعم كامل لاتجاه RTL والخطوط العربية (Cairo) والإنجليزية (Inter)."
              : "Live interactive inspection sandbox for the 3 core LMS domain components with RTL/LTR bilingual testing."}
          </p>
        </div>

        {/* Global Controls */}
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setDirection(d => (d === "ltr" ? "rtl" : "ltr"))}
            className="flex items-center gap-1.5 text-xs font-medium cursor-pointer"
          >
            <GlobeIcon className="size-3.5" />
            <span>{isRtl ? "English (LTR)" : "العربية (RTL)"}</span>
          </Button>
        </div>
      </div>

      {/* Grid: Component 1 & Component 2 */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* ========================================================================= */}
        {/* COMPONENT 1: BitmaskProgressBar */}
        {/* ========================================================================= */}
        <Card className="border-border/80 shadow-xs flex flex-col">
          <CardHeader className="pb-3 text-start">
            <div className="flex items-center justify-between">
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <SlidersIcon className="size-4 text-primary" />
                <span>1. Bitmask Progress Bar</span>
              </CardTitle>
              <Badge variant="secondary" className="font-mono text-xs">
                VARBIT: {bitmask.length} bits
              </Badge>
            </div>
            <CardDescription className="text-xs">
              {isRtl
                ? "شريط تتبع التقدم التفاعلي المستند إلى سلسلة البتات (11110010) من قاعدة البيانات."
                : "Completion indicator driven by database VARBIT bitmask string (1 = done, 0 = locked)."}
            </CardDescription>
          </CardHeader>

          <CardContent className="space-y-6 flex-1 text-start">
            {/* Live Component Preview Box */}
            <div className="p-5 rounded-xl border border-border/60 bg-muted/20 space-y-4">
              <span className="text-[11px] font-mono text-muted-foreground uppercase tracking-wider block">
                Live Component Preview
              </span>

              <BitmaskProgressBar
                bitmask={bitmask}
                totalLessons={bitmask.length}
                variant={bitmaskVariant}
                size={bitmaskSize}
                label={isRtl ? "التقدم في المحتوى" : "Curriculum Progress"}
              />
            </div>

            {/* Interactive Bit Toggles */}
            <div className="space-y-2">
              <Label className="text-xs text-muted-foreground flex items-center justify-between">
                <span>Interactive Bitmask Flip (Click bits to toggle 1/0):</span>
                <span className="font-mono text-foreground font-semibold">"{bitmask}"</span>
              </Label>
              <div className="flex items-center gap-1.5 flex-wrap">
                {Array.from({ length: Math.min(bitmask.length, 16) }, (_, i) => {
                  const isSet = bitmask.charAt(i) === "1";
                  return (
                    <button
                      key={i}
                      type="button"
                      onClick={() => toggleBit(i)}
                      className={`size-8 rounded-md font-mono text-xs font-bold transition-all border cursor-pointer ${
                        isSet
                          ? "bg-primary text-primary-foreground border-primary shadow-xs"
                          : "bg-card text-muted-foreground border-border hover:border-primary/40"
                      }`}
                      title={`Bit ${i}: ${isSet ? "1 (Complete)" : "0 (Locked)"}`}
                    >
                      {isSet ? "1" : "0"}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Controls */}
            <div className="grid grid-cols-2 gap-3 pt-2 border-t border-border/40">
              <div className="space-y-1">
                <Label className="text-xs">Variant</Label>
                <div className="flex rounded-lg border border-border p-0.5 bg-muted/30">
                  <button
                    type="button"
                    onClick={() => setBitmaskVariant("continuous")}
                    className={`flex-1 py-1 text-xs rounded-md font-medium transition-colors cursor-pointer ${
                      bitmaskVariant === "continuous"
                        ? "bg-background text-foreground shadow-xs font-semibold"
                        : "text-muted-foreground hover:text-foreground"
                    }`}
                  >
                    Continuous
                  </button>
                  <button
                    type="button"
                    onClick={() => setBitmaskVariant("segmented")}
                    className={`flex-1 py-1 text-xs rounded-md font-medium transition-colors cursor-pointer ${
                      bitmaskVariant === "segmented"
                        ? "bg-background text-foreground shadow-xs font-semibold"
                        : "text-muted-foreground hover:text-foreground"
                    }`}
                  >
                    Segmented
                  </button>
                </div>
              </div>

              <div className="space-y-1">
                <Label className="text-xs">Size</Label>
                <div className="flex rounded-lg border border-border p-0.5 bg-muted/30">
                  {(["sm", "md", "lg"] as const).map(s => (
                    <button
                      key={s}
                      type="button"
                      onClick={() => setBitmaskSize(s)}
                      className={`flex-1 py-1 text-xs rounded-md font-medium uppercase transition-colors cursor-pointer ${
                        bitmaskSize === s
                          ? "bg-background text-foreground shadow-xs font-semibold"
                          : "text-muted-foreground hover:text-foreground"
                      }`}
                    >
                      {s}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* ========================================================================= */}
        {/* COMPONENT 2: VoucherInput */}
        {/* ========================================================================= */}
        <Card className="border-border/80 shadow-xs flex flex-col">
          <CardHeader className="pb-3 text-start">
            <div className="flex items-center justify-between">
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <KeyRoundIcon className="size-4 text-primary" />
                <span>2. Physical Voucher Code Input</span>
              </CardTitle>
              <Badge variant="secondary" className="font-mono text-xs">
                OTP 4x4
              </Badge>
            </div>
            <CardDescription className="text-xs">
              {isRtl
                ? "حقل إدخال البطاقات الفيزيائية السورية المجزأ مع بادئة [AZ] والتطبيع التلقائي."
                : "Segmented OTP input for physical scratch cards with [AZ] prefix and auto-uppercase paste."}
            </CardDescription>
          </CardHeader>

          <CardContent className="space-y-6 flex-1 text-start">
            {/* Live Component Preview Box */}
            <div className="p-6 rounded-xl border border-border/60 bg-muted/20 flex flex-col items-center justify-center gap-3">
              <span className="text-[11px] font-mono text-muted-foreground uppercase tracking-wider self-start">
                Live Component Preview
              </span>

              <VoucherInput
                value={voucherCode}
                onChange={setVoucherCode}
                onComplete={handleVoucherComplete}
                prefix="AZ"
                state={voucherState}
                errorMessage={voucherError}
                successMessage={voucherSuccess}
              />

              {lastRedeemedCode && (
                <div className="flex items-center gap-1.5 text-xs text-muted-foreground font-mono bg-background/80 px-2.5 py-1 rounded-md border border-border/60">
                  <span>Full Code:</span>
                  <span className="text-primary font-bold">{lastRedeemedCode}</span>
                </div>
              )}
            </div>

            {/* Validation State Selector */}
            <div className="space-y-1.5">
              <Label className="text-xs">Simulate Validation State</Label>
              <div className="grid grid-cols-4 gap-1.5">
                {(["idle", "valid", "error", "loading"] as const).map(st => (
                  <button
                    key={st}
                    type="button"
                    onClick={() => setVoucherState(st)}
                    className={`py-1.5 text-xs rounded-lg font-medium capitalize border transition-all cursor-pointer ${
                      voucherState === st
                        ? "bg-primary text-primary-foreground border-primary shadow-xs font-bold"
                        : "bg-card text-muted-foreground border-border hover:border-border/80"
                    }`}
                  >
                    {st}
                  </button>
                ))}
              </div>
            </div>

            {/* Quick Fill Simulation Buttons */}
            <div className="space-y-1.5 pt-2 border-t border-border/40">
              <Label className="text-xs text-muted-foreground">Quick Test Fillers:</Label>
              <div className="flex items-center gap-2 flex-wrap">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setVoucherCode("9482K92X");
                    setVoucherState("valid");
                    handleVoucherComplete("AZ-9482-K92X");
                  }}
                  className="text-xs h-7 cursor-pointer"
                >
                  <CopyIcon className="size-3 me-1" />
                  Fill "AZ-9482-K92X" (Valid)
                </Button>

                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setVoucherCode("3189P42A");
                    setVoucherState("error");
                  }}
                  className="text-xs h-7 text-destructive cursor-pointer"
                >
                  Fill Expired Code (Error)
                </Button>

                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    setVoucherCode("");
                    setVoucherState("idle");
                    setLastRedeemedCode(null);
                  }}
                  className="text-xs h-7 cursor-pointer"
                >
                  Reset
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* ========================================================================= */}
      {/* COMPONENT 3: CourseCard */}
      {/* ========================================================================= */}
      <Card className="border-border/80 shadow-xs">
        <CardHeader className="pb-3 text-start">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="space-y-1">
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <LayersIcon className="size-4 text-primary" />
                <span>3. Course Card (Catalog Grid & Dashboard List)</span>
              </CardTitle>
              <CardDescription className="text-xs">
                {isRtl
                  ? "بطاقة الدورة الأساسية الداعمة للتخطيط الشبكي الرأسي 16:9 والتخطيط الأفقي المضغوط."
                  : "Core course card supporting vertical 16:9 grid and horizontal dashboard row layouts."}
              </CardDescription>
            </div>

            {/* Layout & Language Switchers */}
            <div className="flex items-center gap-2 flex-wrap">
              <div className="flex rounded-lg border border-border p-0.5 bg-muted/30">
                <button
                  type="button"
                  onClick={() => setCardLayout("vertical")}
                  className={`px-2.5 py-1 text-xs rounded-md font-medium transition-colors cursor-pointer ${
                    cardLayout === "vertical"
                      ? "bg-background text-foreground shadow-xs font-semibold"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  Vertical Grid
                </button>
                <button
                  type="button"
                  onClick={() => setCardLayout("horizontal")}
                  className={`px-2.5 py-1 text-xs rounded-md font-medium transition-colors cursor-pointer ${
                    cardLayout === "horizontal"
                      ? "bg-background text-foreground shadow-xs font-semibold"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  Horizontal Row
                </button>
              </div>

              <div className="flex rounded-lg border border-border p-0.5 bg-muted/30">
                <button
                  type="button"
                  onClick={() => setIsEnrolled(true)}
                  className={`px-2 py-1 text-xs rounded-md font-medium transition-colors cursor-pointer ${
                    isEnrolled
                      ? "bg-background text-foreground shadow-xs font-semibold"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  Enrolled
                </button>
                <button
                  type="button"
                  onClick={() => setIsEnrolled(false)}
                  className={`px-2 py-1 text-xs rounded-md font-medium transition-colors cursor-pointer ${
                    !isEnrolled
                      ? "bg-background text-foreground shadow-xs font-semibold"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  Locked / New
                </button>
              </div>

              <div className="flex rounded-lg border border-border p-0.5 bg-muted/30">
                <button
                  type="button"
                  onClick={() => setCourseLanguage("en")}
                  className={`px-2 py-1 text-xs rounded-md font-medium transition-colors cursor-pointer ${
                    courseLanguage === "en"
                      ? "bg-background text-foreground shadow-xs font-semibold"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  English
                </button>
                <button
                  type="button"
                  onClick={() => setCourseLanguage("ar")}
                  className={`px-2 py-1 text-xs rounded-md font-medium transition-colors cursor-pointer ${
                    courseLanguage === "ar"
                      ? "bg-background text-foreground shadow-xs font-semibold"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  العربية
                </button>
              </div>
            </div>
          </div>
        </CardHeader>

        <CardContent className="space-y-4 text-start">
          <div className="p-6 rounded-xl border border-border/60 bg-muted/20">
            <span className="text-[11px] font-mono text-muted-foreground uppercase tracking-wider block mb-4">
              Live Component Preview ({cardLayout} • {isEnrolled ? "Student Enrolled" : "Public Catalog"})
            </span>

            <div className={cardLayout === "vertical" ? "max-w-sm mx-auto sm:mx-0" : "w-full"}>
              <CourseCard
                layout={cardLayout}
                isEnrolled={isEnrolled}
                title={
                  courseLanguage === "ar"
                    ? "الجبر الخطي المتقدم وتطبيقات الفضاءات المتجهية"
                    : "Advanced Linear Algebra & Vector Space Applications"
                }
                category={courseLanguage === "ar" ? "الرياضيات التطبيقية" : "Mathematics Track"}
                instructor={{
                  name: courseLanguage === "ar" ? "د. طارق الحسان" : "Dr. Tariq Al-Hassan",
                }}
                lessonCount={12}
                duration={courseLanguage === "ar" ? "٤.٥ ساعات" : "4.5 hrs"}
                price={courseLanguage === "ar" ? "رمز مكتبة فيزيائي" : "Physical Voucher"}
                progress={
                  isEnrolled
                    ? { bitmask: bitmask, totalLessons: 12 }
                    : undefined
                }
                ctaLabel={
                  isEnrolled
                    ? courseLanguage === "ar" ? "متابعة الدرس ٧" : "Continue Lesson 7"
                    : courseLanguage === "ar" ? "تفعيل برمز المكتبة" : "Unlock with Library Code"
                }
                onCtaClick={() => {
                  toast.success(
                    isEnrolled
                      ? "Navigating to next lesson player..."
                      : "Opening physical voucher redemption modal..."
                  );
                }}
              />
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
