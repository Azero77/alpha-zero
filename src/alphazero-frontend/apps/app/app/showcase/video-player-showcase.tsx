"use client";

import * as React from "react";
import {
  VideoPlayer,
  DynamicVisibleWatermark,
  type WatermarkData,
} from "@repo/design-system";
import { Button } from "@repo/design-system/components/ui/button";
import { Input } from "@repo/design-system/components/ui/input";
import { Label } from "@repo/design-system/components/ui/label";
import { Badge } from "@repo/design-system/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@repo/design-system/components/ui/card";
import { Switch } from "@repo/design-system/components/ui/switch";
import { Slider } from "@repo/design-system/components/ui/slider";
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@repo/design-system/components/ui/tabs";
import { toast } from "@repo/design-system/components/ui/sonner";
import {
  AlertTriangleIcon,
  CheckCircle2Icon,
  Code2Icon,
  EyeIcon,
  EyeOffIcon,
  GlobeIcon,
  KeyRoundIcon,
  LaptopIcon,
  LayersIcon,
  LockIcon,
  PlayCircleIcon,
  RadioIcon,
  RefreshCwIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
  SlidersIcon,
  SmartphoneIcon,
  SparklesIcon,
  TvIcon,
} from "lucide-react";

interface StreamPreset {
  id: string;
  nameEn: string;
  nameAr: string;
  url: string;
  badge: string;
  descriptionEn: string;
  descriptionAr: string;
}

const STREAM_PRESETS: StreamPreset[] = [
  {
    id: "mux-demo",
    nameEn: "Mux Adaptive Multi-Bitrate HLS",
    nameAr: "بث Mux التكيفي متعدد السرعات (HLS)",
    url: "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8",
    badge: "Public Test • 1080p/720p/480p",
    descriptionEn: "Live adaptive bitrate stream (Big Buck Bunny) verifying smooth fragment buffering and multi-resolution switching.",
    descriptionAr: "بث حي متعدد الدقات لاختبار التبديل السلس وسرعة التخزين المؤقت في ظروف الاتصال المتفاوتة.",
  },
  {
    id: "sintel-demo",
    nameEn: "Sintel Open Movie Stream",
    nameAr: "بث فيلم Sintel عالي الجودة",
    url: "https://bitdash-a.akamaihd.net/content/sintel/hls/playlist.m3u8",
    badge: "VOD HLS • 720p Multi-Audio",
    descriptionEn: "High-contrast cinematic content ideal for inspecting watermark readability against varying dark and bright backgrounds.",
    descriptionAr: "محتوى سينمائي بتباين لوني مرتفع لاختبار مقروئية العلامة المائية فوق المشاهد المظلمة والمضيئة.",
  },
  {
    id: "bff-demo",
    nameEn: "AlphaZero Next.js BFF Manifest Proxy",
    nameAr: "مسار وسيط Next.js BFF المباشر",
    url: "/api/player/3f122add-53ef-46fb-ad9e-6ea407db191a/master",
    badge: "BFF Route • AES-128 Key Ticket",
    descriptionEn: "Demonstrates the Next.js manifest rewriting proxy route that injects short-lived (120s) AES-128 decryption key tickets.",
    descriptionAr: "مسار الـ BFF لإعادة كتابة قوائم التشغيل وحقن تذاكر فك التشفير اللحظية (١٢٠ ثانية).",
  },
];

export function VideoPlayerShowcaseClient() {
  const [direction, setDirection] = React.useState<"ltr" | "rtl">("ltr");
  const isRtl = direction === "rtl";

  // Stream state
  const [selectedPreset, setSelectedPreset] = React.useState<string>("mux-demo");
  const [manifestUrl, setManifestUrl] = React.useState<string>(
    STREAM_PRESETS[0].url
  );
  const [customUrlInput, setCustomUrlInput] = React.useState<string>("");

  // Container viewport simulation
  const [deviceViewport, setDeviceViewport] = React.useState<
    "desktop" | "tablet" | "mobile"
  >("desktop");

  // Watermark state
  const [showWatermark, setShowWatermark] = React.useState<boolean>(true);
  const [watermarkOpacity, setWatermarkOpacity] = React.useState<number>(0.24);
  const [studentName, setStudentName] = React.useState<string>("Ahmad Al-Khatib");
  const [studentPhone, setStudentPhone] = React.useState<string>("+963 991 234 567");
  const [tenantName, setTenantName] = React.useState<string>("AlphaZero Damascus");
  const [sessionId, setSessionId] = React.useState<string>("sess-7fa85f64-9b21");
  const [studentId, setStudentId] = React.useState<string>("usr-48912-sy");

  // Playback telemetry state
  const [currentTime, setCurrentTime] = React.useState<number>(0);
  const [duration, setDuration] = React.useState<number>(0);
  const [playerKey, setPlayerKey] = React.useState<number>(1);
  const [simulated410Count, setSimulated410Count] = React.useState<number>(0);

  const watermarkContext: WatermarkData = React.useMemo(
    () => ({
      userId: studentId,
      userName: studentName,
      userPhone: studentPhone,
      tenantId: tenantName,
      sessionId: sessionId,
    }),
    [studentId, studentName, studentPhone, tenantName, sessionId]
  );

  const formatTime = (seconds: number) => {
    if (isNaN(seconds) || seconds <= 0) return "00:00";
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins.toString().padStart(2, "0")}:${secs.toString().padStart(2, "0")}`;
  };

  const handleSelectPreset = (preset: StreamPreset) => {
    setSelectedPreset(preset.id);
    setManifestUrl(preset.url);
    setPlayerKey((k) => k + 1);
    toast.info(isRtl ? `تم تبديل البث: ${preset.nameAr}` : `Switched stream: ${preset.nameEn}`);
  };

  const handleApplyCustomUrl = () => {
    if (!customUrlInput.trim()) {
      toast.error(isRtl ? "الرجاء إدخال رابط صالح" : "Please enter a valid URL");
      return;
    }
    setSelectedPreset("custom");
    setManifestUrl(customUrlInput.trim());
    setPlayerKey((k) => k + 1);
    toast.success(isRtl ? "تم تحميل الرابط المخصص" : "Loaded custom stream URL");
  };

  const handleRandomizeStudent = () => {
    const names = [
      { en: "Ahmad Al-Khatib", ar: "أحمد الخطيب", phone: "+963 991 234 567" },
      { en: "Sara Al-Dimashqi", ar: "سارة الدمشقي", phone: "+963 988 765 432" },
      { en: "Omar Al-Halabi", ar: "عمر الحلبي", phone: "+963 933 112 233" },
      { en: "Nour Al-Huda", ar: "نور الهدى", phone: "+963 944 556 677" },
    ];
    const picked = names[Math.floor(Math.random() * names.length)];
    setStudentName(isRtl ? picked.ar : picked.en);
    setStudentPhone(picked.phone);
    const newSession = `sess-${Math.random().toString(36).substring(2, 10)}`;
    setSessionId(newSession);
    toast.success(
      isRtl ? `تم تعيين الطالب: ${picked.ar}` : `Assigned student: ${picked.en}`
    );
  };

  const handleSimulate410Expiry = () => {
    setSimulated410Count((c) => c + 1);
    toast.warning(
      isRtl
        ? "محاكاة: انتهت صلاحية تذكرة فك التشفير (HTTP 410) — المشغل يُعيد تحميل القائمة تلقائياً"
        : "Simulated: Key Ticket Expired (HTTP 410) — Player auto-reloads playlist to mint fresh ticket"
    );
  };

  return (
    <div
      dir={direction}
      className="space-y-8 p-4 md:p-8 max-w-7xl mx-auto transition-all"
    >
      {/* Top Banner & Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 p-5 rounded-2xl border border-border/80 bg-card/60 backdrop-blur-sm shadow-xs">
        <div className="space-y-1 text-start">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="p-1.5 rounded-lg bg-primary/15 text-primary">
              <ShieldCheckIcon className="size-4" />
            </span>
            <h1 className="text-xl font-bold tracking-tight text-foreground">
              {isRtl
                ? "مشغل الفيديو الآمن ومختبر العلامات المائية"
                : "AlphaZero Secure Video Player & Watermark Engine"}
            </h1>
            <Badge variant="default" className="font-mono text-xs bg-emerald-600 text-white hover:bg-emerald-700">
              Zero-Trust HLS • Active
            </Badge>
          </div>
          <p className="text-xs text-muted-foreground">
            {isRtl
              ? "بيئة اختبار تفاعلية متقدمة لمشغل الفيديو المشفر (HLS)، والعلامة المائية العائمة بحركة براونية ثنائية الأبعاد (2D Brownian Motion)، ونظام حماية الـ CDN."
              : "Live interactive inspection sandbox for encrypted HLS streaming, 2D Brownian motion canvas watermarking, and Cloudflare capability tokens."}
          </p>
        </div>

        {/* Global Controls */}
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setDirection((d) => (d === "ltr" ? "rtl" : "ltr"))}
            className="flex items-center gap-1.5 text-xs font-medium cursor-pointer"
          >
            <GlobeIcon className="size-3.5" />
            <span>{isRtl ? "English (LTR)" : "العربية (RTL)"}</span>
          </Button>
        </div>
      </div>

      {/* Main Grid: Player Stage & Control Hub */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
        {/* ========================================================================= */}
        {/* LEFT/MAIN STAGE: Live Video Player Preview (7 cols on lg) */}
        {/* ========================================================================= */}
        <div className="lg:col-span-7 space-y-4">
          <Card className="border-border/80 shadow-xs overflow-hidden">
            <CardHeader className="pb-3 text-start">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div className="space-y-0.5">
                  <CardTitle className="text-base font-semibold flex items-center gap-2">
                    <PlayCircleIcon className="size-4 text-primary" />
                    <span>{isRtl ? "شاشة المشغل التفاعلي" : "Live Video Player Stage"}</span>
                  </CardTitle>
                  <CardDescription className="text-xs font-mono truncate max-w-md">
                    {manifestUrl}
                  </CardDescription>
                </div>

                {/* Viewport Simulation Selector */}
                <div className="flex items-center gap-1 bg-muted/40 p-1 rounded-lg border border-border/50">
                  <Button
                    type="button"
                    variant={deviceViewport === "desktop" ? "secondary" : "ghost"}
                    size="sm"
                    className="h-7 px-2 text-xs gap-1"
                    onClick={() => setDeviceViewport("desktop")}
                    title="Desktop 16:9"
                  >
                    <TvIcon className="size-3.5" />
                    <span className="hidden sm:inline">16:9</span>
                  </Button>
                  <Button
                    type="button"
                    variant={deviceViewport === "tablet" ? "secondary" : "ghost"}
                    size="sm"
                    className="h-7 px-2 text-xs gap-1"
                    onClick={() => setDeviceViewport("tablet")}
                    title="Tablet Viewport"
                  >
                    <LaptopIcon className="size-3.5" />
                    <span className="hidden sm:inline">Tablet</span>
                  </Button>
                  <Button
                    type="button"
                    variant={deviceViewport === "mobile" ? "secondary" : "ghost"}
                    size="sm"
                    className="h-7 px-2 text-xs gap-1"
                    onClick={() => setDeviceViewport("mobile")}
                    title="Mobile Viewport"
                  >
                    <SmartphoneIcon className="size-3.5" />
                    <span className="hidden sm:inline">Mobile</span>
                  </Button>
                </div>
              </div>
            </CardHeader>

            <CardContent className="space-y-4 text-start">
              {/* Responsive Container simulation wrapper */}
              <div
                className={`mx-auto transition-all duration-300 ${
                  deviceViewport === "desktop"
                    ? "w-full"
                    : deviceViewport === "tablet"
                      ? "max-w-xl"
                      : "max-w-sm"
                }`}
              >
                <div className="relative rounded-xl overflow-hidden border border-border/80 shadow-md bg-black">
                  <VideoPlayer
                    key={playerKey}
                    manifestUrl={manifestUrl}
                    watermarkContext={watermarkContext}
                    watermarkOpacity={watermarkOpacity}
                    showWatermark={showWatermark}
                    onTimeUpdate={(cur, dur) => {
                      setCurrentTime(cur);
                      setDuration(dur);
                    }}
                    className="w-full"
                  />
                </div>
              </div>

              {/* Playback Status Bar & Live Telemetry */}
              <div className="flex flex-wrap items-center justify-between gap-3 p-3 rounded-lg border border-border/60 bg-muted/20 text-xs">
                <div className="flex items-center gap-4">
                  <div className="flex items-center gap-1.5">
                    <span className="size-2 rounded-full bg-emerald-500 animate-pulse" />
                    <span className="font-medium text-foreground">
                      {isRtl ? "البث المباشر: جاهز" : "Stream: Active"}
                    </span>
                  </div>
                  <div className="font-mono text-muted-foreground">
                    {formatTime(currentTime)} / {formatTime(duration)}
                  </div>
                </div>

                <div className="flex items-center gap-3 font-mono text-[11px] text-muted-foreground">
                  <span>HLS.js: {typeof window !== "undefined" && window.Hls?.isSupported() ? "MSE v1.5" : "Native HLS"}</span>
                  <span>•</span>
                  <span>Watermark: {showWatermark ? `${Math.round(watermarkOpacity * 100)}%` : "Off"}</span>
                  {simulated410Count > 0 && (
                    <>
                      <span>•</span>
                      <span className="text-amber-500 font-semibold">410 Reloads: {simulated410Count}</span>
                    </>
                  )}
                </div>
              </div>

              {/* Stream Preset Quick Buttons */}
              <div className="space-y-2 pt-2">
                <Label className="text-xs font-semibold text-muted-foreground">
                  {isRtl ? "اختر مصدراً للاختبار الفوري:" : "Select Test Stream Preset:"}
                </Label>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                  {STREAM_PRESETS.map((preset) => {
                    const isCurrent = selectedPreset === preset.id;
                    return (
                      <button
                        key={preset.id}
                        type="button"
                        onClick={() => handleSelectPreset(preset)}
                        className={`p-2.5 rounded-xl border text-start transition-all cursor-pointer flex flex-col justify-between gap-1.5 ${
                          isCurrent
                            ? "bg-primary/10 border-primary/60 text-foreground shadow-xs"
                            : "bg-card hover:bg-muted/30 border-border text-muted-foreground"
                        }`}
                      >
                        <div className="space-y-0.5">
                          <p className="text-xs font-bold text-foreground line-clamp-1">
                            {isRtl ? preset.nameAr : preset.nameEn}
                          </p>
                          <Badge
                            variant={isCurrent ? "default" : "outline"}
                            className="text-[10px] py-0 px-1.5"
                          >
                            {preset.badge}
                          </Badge>
                        </div>
                        <p className="text-[11px] text-muted-foreground line-clamp-2">
                          {isRtl ? preset.descriptionAr : preset.descriptionEn}
                        </p>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Custom Stream URL Input */}
              <div className="pt-2 border-t border-border/40 space-y-2">
                <Label className="text-xs">
                  {isRtl ? "أو أدخل رابط .m3u8 مخصص:" : "Or Enter Custom HLS Manifest (.m3u8) URL:"}
                </Label>
                <div className="flex gap-2">
                  <Input
                    placeholder="https://example.com/stream/master.m3u8"
                    value={customUrlInput}
                    onChange={(e) => setCustomUrlInput(e.target.value)}
                    className="text-xs font-mono h-9 flex-1"
                  />
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={handleApplyCustomUrl}
                    className="h-9 px-3 text-xs gap-1.5"
                  >
                    <RefreshCwIcon className="size-3.5" />
                    <span>{isRtl ? "تحميل" : "Load"}</span>
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Fault Injection & Recovery Simulator Card */}
          <Card className="border-border/80 shadow-xs">
            <CardHeader className="pb-3 text-start">
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <ShieldAlertIcon className="size-4 text-amber-500" />
                <span>{isRtl ? "محاكاة الأعطال والتعافي التلقائي" : "Fault Injection & Auto-Recovery Sandbox"}</span>
              </CardTitle>
              <CardDescription className="text-xs">
                {isRtl
                  ? "اختبار استجابة المشغل للحالات الاستثنائية مثل انتهاء صلاحية التذاكر أو ضعف الاتصال."
                  : "Validate player resilience against edge token expirations and network degradation."}
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 text-start">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleSimulate410Expiry}
                  className="text-xs h-9 justify-start gap-2 border-amber-500/30 hover:bg-amber-500/10 text-amber-600 dark:text-amber-400"
                >
                  <KeyRoundIcon className="size-3.5" />
                  <span>{isRtl ? "محاكاة انتهاء مفتاح 410" : "Simulate 410 Key Expiry"}</span>
                </Button>

                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    toast.info(
                      isRtl
                        ? "المشغل مضبوط على سقف تخزين ٣٠ ثانية / ٣٠ ميغابايت لمنع استهلاك باقات البيانات في سوريا"
                        : "Player configured for 30s / 30MB buffer cap optimized for low-bandwidth Syrian networks"
                    );
                  }}
                  className="text-xs h-9 justify-start gap-2 border-blue-500/30 hover:bg-blue-500/10 text-blue-600 dark:text-blue-400"
                >
                  <RadioIcon className="size-3.5" />
                  <span>{isRtl ? "فحص إعدادات الباندويث المنخفض" : "Check Low-Bandwidth Profile"}</span>
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* ========================================================================= */}
        {/* RIGHT COLUMN: Watermark & Security Controls (5 cols on lg) */}
        {/* ========================================================================= */}
        <div className="lg:col-span-5 space-y-4">
          <Tabs defaultValue="watermark" className="w-full">
            <TabsList className="grid grid-cols-2 w-full">
              <TabsTrigger value="watermark" className="text-xs gap-1.5">
                <SparklesIcon className="size-3.5" />
                <span>{isRtl ? "العلامة المائية" : "Watermark Lab"}</span>
              </TabsTrigger>
              <TabsTrigger value="security" className="text-xs gap-1.5">
                <LockIcon className="size-3.5" />
                <span>{isRtl ? "هندسة الأمان" : "Security Specs"}</span>
              </TabsTrigger>
            </TabsList>

            {/* TAB 1: Watermark Controls */}
            <TabsContent value="watermark" className="space-y-4 mt-3">
              <Card className="border-border/80 shadow-xs">
                <CardHeader className="pb-3 text-start">
                  <div className="flex items-center justify-between">
                    <CardTitle className="text-base font-semibold flex items-center gap-2">
                      <SlidersIcon className="size-4 text-primary" />
                      <span>{isRtl ? "إعدادات العلامة المائية" : "Dynamic Watermark Controls"}</span>
                    </CardTitle>
                    <div className="flex items-center gap-2">
                      <Switch
                        id="wm-toggle"
                        checked={showWatermark}
                        onCheckedChange={setShowWatermark}
                      />
                      <Label htmlFor="wm-toggle" className="text-xs font-mono cursor-pointer">
                        {showWatermark ? (
                          <span className="text-emerald-500 flex items-center gap-1">
                            <EyeIcon className="size-3" /> ON
                          </span>
                        ) : (
                          <span className="text-muted-foreground flex items-center gap-1">
                            <EyeOffIcon className="size-3" /> OFF
                          </span>
                        )}
                      </Label>
                    </div>
                  </div>
                  <CardDescription className="text-xs">
                    {isRtl
                      ? "تعديل هوية الطالب والشفافية لملاحظة التموضع العشوائي للحركة البراونية."
                      : "Adjust student identity and opacity to inspect 2D Brownian drift behavior."}
                  </CardDescription>
                </CardHeader>

                <CardContent className="space-y-5 text-start">
                  {/* Opacity Slider */}
                  <div className="space-y-2 p-3 rounded-xl border border-border/60 bg-muted/20">
                    <div className="flex items-center justify-between">
                      <Label className="text-xs font-medium">
                        {isRtl ? "درجة الشفافية (Opacity):" : "Watermark Opacity:"}
                      </Label>
                      <span className="font-mono text-xs font-bold text-primary">
                        {Math.round(watermarkOpacity * 100)}%
                      </span>
                    </div>
                    <Slider
                      value={[watermarkOpacity * 100]}
                      min={5}
                      max={50}
                      step={1}
                      onValueChange={(val) => setWatermarkOpacity((val[0] || 22) / 100)}
                    />
                    <div className="flex justify-between text-[10px] text-muted-foreground font-mono">
                      <span>5% (Ultra Subtle)</span>
                      <span>22% (Recommended)</span>
                      <span>50% (Forensic Audit)</span>
                    </div>
                  </div>

                  {/* Identity Fields */}
                  <div className="space-y-3">
                    <div className="flex items-center justify-between">
                      <Label className="text-xs font-semibold text-muted-foreground">
                        {isRtl ? "بيانات هوية الطالب:" : "Student Forensic Identity:"}
                      </Label>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={handleRandomizeStudent}
                        className="h-6 px-2 text-[11px] text-primary"
                      >
                        <RefreshCwIcon className="size-3 me-1" />
                        {isRtl ? "توليد طالب جديد" : "Randomize"}
                      </Button>
                    </div>

                    <div className="space-y-1.5">
                      <Label className="text-[11px] text-muted-foreground">Student Name</Label>
                      <Input
                        value={studentName}
                        onChange={(e) => setStudentName(e.target.value)}
                        className="text-xs h-8"
                      />
                    </div>

                    <div className="grid grid-cols-2 gap-2">
                      <div className="space-y-1.5">
                        <Label className="text-[11px] text-muted-foreground">Phone / Device</Label>
                        <Input
                          value={studentPhone}
                          onChange={(e) => setStudentPhone(e.target.value)}
                          className="text-xs h-8 font-mono"
                        />
                      </div>
                      <div className="space-y-1.5">
                        <Label className="text-[11px] text-muted-foreground">Student ID</Label>
                        <Input
                          value={studentId}
                          onChange={(e) => setStudentId(e.target.value)}
                          className="text-xs h-8 font-mono"
                        />
                      </div>
                    </div>

                    <div className="grid grid-cols-2 gap-2">
                      <div className="space-y-1.5">
                        <Label className="text-[11px] text-muted-foreground">Academy / Tenant</Label>
                        <Input
                          value={tenantName}
                          onChange={(e) => setTenantName(e.target.value)}
                          className="text-xs h-8"
                        />
                      </div>
                      <div className="space-y-1.5">
                        <Label className="text-[11px] text-muted-foreground">Session Token</Label>
                        <Input
                          value={sessionId}
                          onChange={(e) => setSessionId(e.target.value)}
                          className="text-xs h-8 font-mono"
                        />
                      </div>
                    </div>
                  </div>

                  {/* Physics & Security Notice Box */}
                  <div className="p-3 rounded-lg border border-primary/20 bg-primary/5 text-xs space-y-1">
                    <p className="font-semibold text-primary flex items-center gap-1.5">
                      <SparklesIcon className="size-3.5" />
                      <span>{isRtl ? "مقاومة برامج إخفاء العلامات المائية" : "Anti-Masking Mathematics"}</span>
                    </p>
                    <p className="text-[11px] text-muted-foreground leading-relaxed">
                      {isRtl
                        ? "تتحرك العلامة عبر نموذج (2D Brownian Motion) مع ارتداد فيزيائي ناعم عن الحواف وطبقة إضافية خفية في الربع المعاكس لمنع القص أو الفلاتر الثابتة."
                        : "Continuous velocity perturbation prevents AI video inpainting / median blend removal. Ghost watermark in opposite quadrant prevents crop circumvention."}
                    </p>
                  </div>
                </CardContent>
              </Card>
            </TabsContent>

            {/* TAB 2: Security Architecture Specs */}
            <TabsContent value="security" className="space-y-4 mt-3">
              <Card className="border-border/80 shadow-xs">
                <CardHeader className="pb-3 text-start">
                  <CardTitle className="text-base font-semibold flex items-center gap-2">
                    <ShieldCheckIcon className="size-4 text-primary" />
                    <span>{isRtl ? "معايير الأمان الأربعة" : "4-Tier Security Matrix"}</span>
                  </CardTitle>
                  <CardDescription className="text-xs">
                    {isRtl
                      ? "الهيكل الدفاعي الشامل لحماية الفيديو دون استهلاك موارد الـ BFF."
                      : "Architectural layers enforcing zero-trust video distribution."}
                  </CardDescription>
                </CardHeader>

                <CardContent className="space-y-3.5 text-start text-xs">
                  {/* Tier 1 */}
                  <div className="p-3 rounded-xl border border-border/70 bg-card space-y-1">
                    <div className="flex items-center justify-between">
                      <span className="font-bold text-foreground">Tier 1: Cloudflare Edge Cookie</span>
                      <Badge variant="outline" className="font-mono text-[10px]">az_vid_cap</Badge>
                    </div>
                    <p className="text-[11px] text-muted-foreground">
                      Stateless HMAC-SHA256 cookie scoped strictly to <code className="text-primary font-mono">/streaming/{'{tenantId}'}/{'{videoId}'}/*</code>. Edge Worker verifies token and injects secret header <code className="font-mono">X-Origin-Auth</code> to AWS S3.
                    </p>
                  </div>

                  {/* Tier 2 */}
                  <div className="p-3 rounded-xl border border-border/70 bg-card space-y-1">
                    <div className="flex items-center justify-between">
                      <span className="font-bold text-foreground">Tier 2: Next.js BFF Manifest Rewriter</span>
                      <Badge variant="outline" className="font-mono text-[10px]">Zero Media Proxy</Badge>
                    </div>
                    <p className="text-[11px] text-muted-foreground">
                      Lightweight manifest text manipulation only. Heavy <code className="font-mono">.ts</code> media segments route direct to Cloudflare CDN, eliminating bandwidth and egress costs on Next.js.
                    </p>
                  </div>

                  {/* Tier 3 */}
                  <div className="p-3 rounded-xl border border-border/70 bg-card space-y-1">
                    <div className="flex items-center justify-between">
                      <span className="font-bold text-foreground">Tier 3: Ephemeral Key Tickets</span>
                      <Badge variant="outline" className="font-mono text-[10px]">TTL: 120s</Badge>
                    </div>
                    <p className="text-[11px] text-muted-foreground">
                      HLS.js fetches decryption keys via ephemeral signed ticket routes. Auto-reloads playlist when expired (HTTP 410) to mint fresh keys without interrupting student playback.
                    </p>
                  </div>

                  {/* Tier 4 */}
                  <div className="p-3 rounded-xl border border-border/70 bg-card space-y-1">
                    <div className="flex items-center justify-between">
                      <span className="font-bold text-foreground">Tier 4: Dynamic Canvas Watermark</span>
                      <Badge variant="outline" className="font-mono text-[10px]">Brownian Motion</Badge>
                    </div>
                    <p className="text-[11px] text-muted-foreground">
                      Client-rendered HTML5 canvas layer with dual drifting coordinates dX/dY preventing frame-stacking watermark subtraction or screen recording piracy.
                    </p>
                  </div>
                </CardContent>
              </Card>
            </TabsContent>
          </Tabs>
        </div>
      </div>
    </div>
  );
}
