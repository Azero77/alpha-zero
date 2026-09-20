"use client";

import * as React from "react";
import { useEffect, useRef, useState, useId, useCallback } from "react";
import {
  DynamicVisibleWatermark,
  type WatermarkData,
} from "./dynamic-visible-watermark";
import type { VideoPlayerProps, VideoQualityLevel, PlaybackRate } from "./video-player-types";
import { cn } from "@repo/design-system/lib/utils";
import {
  AlertCircleIcon,
  CheckIcon,
  ChevronRightIcon,
  CornerDownRightIcon,
  EyeIcon,
  EyeOffIcon,
  FastForwardIcon,
  GaugeIcon,
  Maximize2Icon,
  Minimize2Icon,
  PauseIcon,
  PlayIcon,
  RotateCcwIcon,
  RotateCwIcon,
  SettingsIcon,
  ShieldCheckIcon,
  SlidersHorizontalIcon,
  SparklesIcon,
  TvIcon,
  Volume1Icon,
  Volume2Icon,
  VolumeXIcon,
  WifiIcon,
  WifiOffIcon,
} from "lucide-react";

// Hls.js interface declaration
interface HlsInstance {
  loadSource(url: string): void;
  attachMedia(media: HTMLMediaElement): void;
  destroy(): void;
  on(event: string, callback: (...args: any[]) => void): void;
  recoverMediaError(): void;
  startLoad(): void;
  levels: Array<{
    height: number;
    width: number;
    bitrate: number;
    name?: string;
    attrs?: Record<string, string>;
  }>;
  currentLevel: number;
  loadLevel: number;
  autoLevelCapping: number;
  autoLevelEnabled: boolean;
}

declare global {
  interface Window {
    Hls?: {
      isSupported(): boolean;
      new (config?: any): HlsInstance;
      Events: {
        MANIFEST_PARSED: string;
        ERROR: string;
        LEVEL_LOADED: string;
        LEVEL_SWITCHED: string;
      };
      ErrorTypes: {
        NETWORK_ERROR: string;
        MEDIA_ERROR: string;
        KEY_SYSTEM_ERROR: string;
        OTHER_ERROR: string;
      };
    };
  }
}

const PLAYBACK_RATES: PlaybackRate[] = [0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];

export function VideoPlayer({
  manifestUrl,
  watermarkContext,
  watermarkOpacity = 0.22,
  showWatermark = true,
  poster,
  title,
  autoPlay = false,
  initialDataSaver = false,
  isTheater = false,
  onTheaterToggle,
  onEnded,
  onTimeUpdate,
  className = "",
}: VideoPlayerProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const hlsRef = useRef<HlsInstance | null>(null);
  const timelineRef = useRef<HTMLDivElement | null>(null);
  const controlsTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  // Playback state
  const [isPlaying, setIsPlaying] = useState<boolean>(false);
  const [currentTime, setCurrentTime] = useState<number>(0);
  const [duration, setDuration] = useState<number>(0);
  const [bufferedPct, setBufferedPct] = useState<number>(0);
  const [volume, setVolume] = useState<number>(1);
  const [isMuted, setIsMuted] = useState<boolean>(false);
  const [playbackRate, setPlaybackRate] = useState<PlaybackRate>(1);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Resolution & ABR state
  const [qualityLevels, setQualityLevels] = useState<VideoQualityLevel[]>([]);
  const [selectedQuality, setSelectedQuality] = useState<number>(-1); // -1 = Auto
  const [activeQualityIndex, setActiveQualityIndex] = useState<number>(-1);
  const [dataSaverMode, setDataSaverMode] = useState<boolean>(initialDataSaver);

  // UI display state
  const [showControls, setShowControls] = useState<boolean>(true);
  const [isSettingsOpen, setIsSettingsOpen] = useState<boolean>(false);
  const [settingsSubmenu, setSettingsSubmenu] = useState<"root" | "quality" | "speed">("root");
  const [isFullscreen, setIsFullscreen] = useState<boolean>(false);
  const [internalTheater, setInternalTheater] = useState<boolean>(isTheater);

  // Scrubbing & Hover Tooltip
  const [isScrubbing, setIsScrubbing] = useState<boolean>(false);
  const [hoverPosition, setHoverPosition] = useState<number | null>(null);
  const [hoverTime, setHoverTime] = useState<number>(0);
  const [centerAnimation, setCenterAnimation] = useState<"play" | "pause" | null>(null);

  const effectiveTheater = onTheaterToggle ? isTheater : internalTheater;

  // Format seconds to mm:ss or hh:mm:ss
  const formatTime = (seconds: number) => {
    if (isNaN(seconds) || seconds < 0) return "00:00";
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);
    if (h > 0) {
      return `${h}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
    }
    return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
  };

  // Reset/Trigger idle timer to hide controls
  const triggerActivity = useCallback(() => {
    setShowControls(true);
    if (controlsTimeoutRef.current) {
      clearTimeout(controlsTimeoutRef.current);
    }
    if (isPlaying && !isSettingsOpen) {
      controlsTimeoutRef.current = setTimeout(() => {
        setShowControls(false);
      }, 2500);
    }
  }, [isPlaying, isSettingsOpen]);

  // Sync Data Saver mode with HLS Level Capping
  useEffect(() => {
    if (!hlsRef.current) return;
    if (dataSaverMode) {
      // Find level <= 480p or lowest level available
      const levels = hlsRef.current.levels;
      if (levels && levels.length > 0) {
        let capIndex = 0;
        for (let i = 0; i < levels.length; i++) {
          if (levels[i].height <= 480) {
            capIndex = Math.max(capIndex, i);
          }
        }
        hlsRef.current.autoLevelCapping = capIndex;
      }
    } else {
      hlsRef.current.autoLevelCapping = -1; // Uncapped
    }
  }, [dataSaverMode]);

  // Initialize Hls.js Player
  useEffect(() => {
    const video = videoRef.current;
    if (!video || !manifestUrl) return;

    setError(null);
    setIsLoading(true);
    setQualityLevels([]);
    setSelectedQuality(-1);

    const initHls = () => {
      // 1. MediaSource Extensions with Hls.js
      if (typeof window !== "undefined" && window.Hls?.isSupported()) {
        const Hls = window.Hls;
        const hls = new Hls({
          xhrSetup: (xhr: XMLHttpRequest) => {
            xhr.withCredentials = true; // Forward signed edge capability cookies
          },
          maxBufferLength: 30, // 30s buffer for Syrian low-bandwidth networks
          maxMaxBufferLength: 60,
          maxBufferSize: 30 * 1000 * 1000, // 30MB memory cap
          enableWorker: true,
          lowLatencyMode: false,
        });

        hlsRef.current = hls;

        hls.loadSource(manifestUrl);
        hls.attachMedia(video);

        hls.on(Hls.Events.MANIFEST_PARSED, (_event, data) => {
          setIsLoading(false);
          const parsedLevels = (hls.levels || data?.levels || []).map((lvl: any, idx: number) => ({
            index: idx,
            height: lvl.height || 0,
            width: lvl.width || 0,
            bitrate: lvl.bitrate || 0,
            label: lvl.height ? `${lvl.height}p` : `${Math.round((lvl.bitrate || 0) / 1000)} kbps`,
          }));

          // Sort descending by height / bitrate
          parsedLevels.sort((a: VideoQualityLevel, b: VideoQualityLevel) => b.height - a.height || b.bitrate - a.bitrate);
          setQualityLevels(parsedLevels);

          if (autoPlay) {
            video.play().catch(() => {});
          }
        });

        hls.on(Hls.Events.LEVEL_SWITCHED, (_event, data) => {
          setActiveQualityIndex(data.level);
        });

        hls.on(Hls.Events.LEVEL_LOADED, (_event, data) => {
          setActiveQualityIndex(data.level);
        });

        hls.on(Hls.Events.ERROR, (_event, data) => {
          if (data.fatal) {
            switch (data.type) {
              case Hls.ErrorTypes.NETWORK_ERROR:
                // Handle 410 Expired Ticket (auto-reloads playlist to mint fresh ticket)
                if (data.response?.code === 410) {
                  hls.startLoad();
                  return;
                }
                hls.startLoad();
                break;
              case Hls.ErrorTypes.MEDIA_ERROR:
                hls.recoverMediaError();
                break;
              default:
                hls.destroy();
                setError("Playback error: Unable to stream media.");
                break;
            }
          }
        });
      } else if (video.canPlayType("application/vnd.apple.mpegurl")) {
        // Native Safari/iOS HLS
        video.src = manifestUrl;
        setIsLoading(false);
      } else {
        video.src = manifestUrl;
        setIsLoading(false);
      }
    };

    if (typeof window !== "undefined" && !window.Hls && !video.canPlayType("application/vnd.apple.mpegurl")) {
      const script = document.createElement("script");
      script.src = "https://cdn.jsdelivr.net/npm/hls.js@1.5.17/dist/hls.min.js";
      script.async = true;
      script.onload = () => initHls();
      script.onerror = () => {
        setError("Failed to initialize HLS streaming core.");
        setIsLoading(false);
      };
      document.body.appendChild(script);

      return () => {
        if (script.parentNode) script.parentNode.removeChild(script);
      };
    } else {
      initHls();
    }

    return () => {
      if (hlsRef.current) {
        hlsRef.current.destroy();
        hlsRef.current = null;
      }
    };
  }, [manifestUrl, autoPlay]);

  // Video Native Event Handlers
  const handlePlayPause = () => {
    const video = videoRef.current;
    if (!video) return;
    if (video.paused || video.ended) {
      video.play().then(() => {
        setIsPlaying(true);
        setCenterAnimation("play");
        setTimeout(() => setCenterAnimation(null), 600);
      }).catch(() => {});
    } else {
      video.pause();
      setIsPlaying(false);
      setCenterAnimation("pause");
      setTimeout(() => setCenterAnimation(null), 600);
    }
    triggerActivity();
  };

  const handleSeek = (deltaSeconds: number) => {
    const video = videoRef.current;
    if (!video) return;
    const target = Math.max(0, Math.min(duration, video.currentTime + deltaSeconds));
    video.currentTime = target;
    triggerActivity();
  };

  const handleVolumeChange = (newVolume: number) => {
    const video = videoRef.current;
    if (!video) return;
    const clamped = Math.max(0, Math.min(1, newVolume));
    video.volume = clamped;
    setVolume(clamped);
    if (clamped > 0 && isMuted) {
      video.muted = false;
      setIsMuted(false);
    }
  };

  const handleToggleMute = () => {
    const video = videoRef.current;
    if (!video) return;
    video.muted = !video.muted;
    setIsMuted(video.muted);
  };

  const handleRateChange = (rate: PlaybackRate) => {
    const video = videoRef.current;
    if (!video) return;
    video.playbackRate = rate;
    setPlaybackRate(rate);
    setIsSettingsOpen(false);
    setSettingsSubmenu("root");
  };

  const handleQualitySelect = (qualityIndex: number) => {
    if (!hlsRef.current) return;
    hlsRef.current.currentLevel = qualityIndex;
    setSelectedQuality(qualityIndex);
    setIsSettingsOpen(false);
    setSettingsSubmenu("root");
  };

  const toggleFullscreen = () => {
    const container = containerRef.current;
    if (!container) return;
    if (!document.fullscreenElement) {
      if (container.requestFullscreen) {
        container.requestFullscreen();
      } else if ((container as any).webkitRequestFullscreen) {
        (container as any).webkitRequestFullscreen();
      }
    } else {
      if (document.exitFullscreen) {
        document.exitFullscreen();
      } else if ((document as any).webkitExitFullscreen) {
        (document as any).webkitExitFullscreen();
      }
    }
  };

  const handleToggleTheater = () => {
    if (onTheaterToggle) {
      onTheaterToggle(!isTheater);
    } else {
      setInternalTheater((t) => !t);
    }
  };

  // Fullscreen change listener
  useEffect(() => {
    const onFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };
    document.addEventListener("fullscreenchange", onFullscreenChange);
    document.addEventListener("webkitfullscreenchange", onFullscreenChange);
    return () => {
      document.removeEventListener("fullscreenchange", onFullscreenChange);
      document.removeEventListener("webkitfullscreenchange", onFullscreenChange);
    };
  }, []);

  // Update buffered progress
  const updateBuffered = () => {
    const video = videoRef.current;
    if (!video || !video.duration) return;
    const b = video.buffered;
    if (!b || b.length === 0) {
      setBufferedPct(0);
      return;
    }
    for (let i = b.length - 1; i >= 0; i--) {
      if (b.start(i) <= video.currentTime) {
        setBufferedPct((b.end(i) / video.duration) * 100);
        return;
      }
    }
    setBufferedPct((b.end(b.length - 1) / video.duration) * 100);
  };

  // Timeline scrubber handlers
  const handleTimelineMove = (e: React.MouseEvent<HTMLDivElement>) => {
    const bar = timelineRef.current;
    if (!bar || !duration) return;
    const rect = bar.getBoundingClientRect();
    const pct = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
    setHoverPosition(pct * 100);
    setHoverTime(pct * duration);

    if (isScrubbing && videoRef.current) {
      videoRef.current.currentTime = pct * duration;
    }
  };

  const handleTimelineMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    setIsScrubbing(true);
    handleTimelineMove(e);
  };

  const handleTimelineMouseUp = () => {
    setIsScrubbing(false);
  };

  useEffect(() => {
    const handleGlobalMouseUp = () => setIsScrubbing(false);
    window.addEventListener("mouseup", handleGlobalMouseUp);
    return () => window.removeEventListener("mouseup", handleGlobalMouseUp);
  }, []);

  // Keyboard Shortcuts Listener
  const handleKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    // Ignore if focus is in an input or textarea
    if (["INPUT", "TEXTAREA", "SELECT"].includes((e.target as HTMLElement).tagName)) {
      return;
    }

    switch (e.key) {
      case " ":
      case "k":
        e.preventDefault();
        handlePlayPause();
        break;
      case "ArrowLeft":
        e.preventDefault();
        handleSeek(-5);
        break;
      case "ArrowRight":
        e.preventDefault();
        handleSeek(5);
        break;
      case "j":
        e.preventDefault();
        handleSeek(-10);
        break;
      case "l":
        e.preventDefault();
        handleSeek(10);
        break;
      case "ArrowUp":
        e.preventDefault();
        handleVolumeChange(volume + 0.1);
        break;
      case "ArrowDown":
        e.preventDefault();
        handleVolumeChange(volume - 0.1);
        break;
      case "m":
        e.preventDefault();
        handleToggleMute();
        break;
      case "f":
        e.preventDefault();
        toggleFullscreen();
        break;
      case "t":
        e.preventDefault();
        handleToggleTheater();
        break;
      case "Escape":
        if (isSettingsOpen) {
          setIsSettingsOpen(false);
          setSettingsSubmenu("root");
        }
        break;
    }
  };

  // Label for active quality badge
  const getQualityBadgeLabel = () => {
    if (selectedQuality === -1) {
      if (activeQualityIndex >= 0 && hlsRef.current?.levels?.[activeQualityIndex]) {
        return `Auto (${hlsRef.current.levels[activeQualityIndex].height}p)`;
      }
      return "Auto";
    }
    const current = qualityLevels.find((l) => l.index === selectedQuality);
    return current ? current.label : "Auto";
  };

  return (
    <div
      ref={containerRef}
      tabIndex={0}
      onKeyDown={handleKeyDown}
      onMouseMove={triggerActivity}
      onMouseLeave={() => isPlaying && !isSettingsOpen && setShowControls(false)}
      className={cn(
        "group relative w-full overflow-hidden bg-black select-none outline-none focus-visible:ring-2 focus-visible:ring-primary transition-all duration-300",
        isFullscreen ? "h-screen w-screen rounded-none" : effectiveTheater ? "w-full aspect-[21/9] rounded-xl" : "aspect-video rounded-xl",
        !showControls && isPlaying && "cursor-none",
        className
      )}
    >
      {/* 1. Underlying Native Video Tag (No Browser Controls) */}
      <video
        ref={videoRef}
        poster={poster}
        playsInline
        className="h-full w-full object-contain cursor-pointer"
        onClick={handlePlayPause}
        onDoubleClick={toggleFullscreen}
        onPlay={() => setIsPlaying(true)}
        onPause={() => setIsPlaying(false)}
        onEnded={() => {
          setIsPlaying(false);
          onEnded?.();
        }}
        onTimeUpdate={() => {
          if (videoRef.current) {
            setCurrentTime(videoRef.current.currentTime);
            setDuration(videoRef.current.duration || 0);
            updateBuffered();
            onTimeUpdate?.(videoRef.current.currentTime, videoRef.current.duration || 0);
          }
        }}
        onLoadedMetadata={() => {
          if (videoRef.current) {
            setDuration(videoRef.current.duration || 0);
          }
        }}
        onWaiting={() => setIsLoading(true)}
        onPlaying={() => setIsLoading(false)}
      />

      {/* 2. Floating 2D Brownian Motion Watermark (z-10) */}
      {showWatermark && watermarkContext && (
        <DynamicVisibleWatermark data={watermarkContext} opacity={watermarkOpacity} />
      )}

      {/* 3. Center Animated Play/Pause Ripple Flash (z-20) */}
      {centerAnimation && (
        <div className="absolute inset-0 flex items-center justify-center pointer-events-none z-20">
          <div className="size-20 rounded-full bg-black/60 backdrop-blur-md border border-white/20 flex items-center justify-center text-white scale-100 animate-ping duration-500">
            {centerAnimation === "play" ? (
              <PlayIcon className="size-10 fill-white translate-x-0.5" />
            ) : (
              <PauseIcon className="size-10 fill-white" />
            )}
          </div>
        </div>
      )}

      {/* 4. Buffering / Loading Indicator */}
      {isLoading && !error && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/40 backdrop-blur-xs pointer-events-none z-20">
          <div className="flex flex-col items-center gap-3">
            <div className="size-10 rounded-full border-3 border-primary/20 border-t-primary animate-spin" />
            <span className="text-xs font-mono text-white/80 tracking-wider uppercase">Loading Stream</span>
          </div>
        </div>
      )}

      {/* 5. Error Overlay */}
      {error && (
        <div className="absolute inset-0 flex flex-col items-center justify-center bg-black/90 p-6 text-center z-50">
          <AlertCircleIcon className="size-10 text-destructive mb-3" />
          <p className="text-sm font-semibold text-white">{error}</p>
          <p className="text-xs text-white/60 mt-1 max-w-sm">
            If testing local files, ensure CORS is enabled and manifest paths exist.
          </p>
          <button
            type="button"
            onClick={() => {
              setError(null);
              setIsLoading(true);
              hlsRef.current?.startLoad();
            }}
            className="mt-4 rounded-lg bg-primary px-4 py-2 text-xs font-bold text-primary-foreground hover:bg-primary/90 transition-colors"
          >
            Retry Playback
          </button>
        </div>
      )}

      {/* 6. Top Bar Overlay (Title + Security Badge) */}
      <div
        className={cn(
          "absolute top-0 inset-x-0 p-4 bg-gradient-to-b from-black/85 via-black/40 to-transparent flex items-center justify-between transition-opacity duration-300 z-30 pointer-events-none",
          showControls || !isPlaying ? "opacity-100" : "opacity-0"
        )}
      >
        <div className="flex items-center gap-2 pointer-events-auto">
          {title && (
            <h3 className="text-sm font-semibold text-white truncate max-w-md drop-shadow-md">
              {title}
            </h3>
          )}
        </div>

        <div className="flex items-center gap-2 pointer-events-auto">
          {dataSaverMode && (
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-emerald-950/80 border border-emerald-500/40 text-[10px] font-mono text-emerald-300">
              <WifiIcon className="size-3" /> Syrian Data-Saver ON
            </span>
          )}
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-white/10 border border-white/20 text-[10px] font-mono text-white/80 backdrop-blur-md">
            <ShieldCheckIcon className="size-3 text-emerald-400" /> AES-128
          </span>
        </div>
      </div>

      {/* 7. Settings Glassmorphism Popover (z-40 - Rendered Inside Container to work in Fullscreen) */}
      {isSettingsOpen && (
        <div
          className="absolute bottom-16 right-4 w-64 rounded-2xl border border-white/20 bg-black/85 backdrop-blur-xl p-2 text-white shadow-2xl z-40 text-xs animate-in fade-in zoom-in-95 duration-150"
          onClick={(e) => e.stopPropagation()}
        >
          {settingsSubmenu === "root" && (
            <div className="space-y-1">
              <div className="px-3 py-1.5 font-bold text-white/60 uppercase tracking-wider text-[10px] border-b border-white/10">
                Playback Settings
              </div>

              {/* Quality Submenu trigger */}
              <button
                type="button"
                onClick={() => setSettingsSubmenu("quality")}
                className="w-full flex items-center justify-between px-3 py-2 rounded-xl hover:bg-white/10 transition-colors text-start"
              >
                <div className="flex items-center gap-2">
                  <SlidersHorizontalIcon className="size-3.5 text-white/70" />
                  <span>Quality</span>
                </div>
                <div className="flex items-center gap-1 text-white/60 font-mono">
                  <span>{getQualityBadgeLabel()}</span>
                  <ChevronRightIcon className="size-3.5" />
                </div>
              </button>

              {/* Speed Submenu trigger */}
              <button
                type="button"
                onClick={() => setSettingsSubmenu("speed")}
                className="w-full flex items-center justify-between px-3 py-2 rounded-xl hover:bg-white/10 transition-colors text-start"
              >
                <div className="flex items-center gap-2">
                  <GaugeIcon className="size-3.5 text-white/70" />
                  <span>Playback Speed</span>
                </div>
                <div className="flex items-center gap-1 text-white/60 font-mono">
                  <span>{playbackRate === 1 ? "Normal" : `${playbackRate}x`}</span>
                  <ChevronRightIcon className="size-3.5" />
                </div>
              </button>

              {/* Data Saver Mode Toggle */}
              <button
                type="button"
                onClick={() => setDataSaverMode((m) => !m)}
                className="w-full flex items-center justify-between px-3 py-2 rounded-xl hover:bg-white/10 transition-colors text-start"
              >
                <div className="flex items-center gap-2">
                  {dataSaverMode ? (
                    <WifiIcon className="size-3.5 text-emerald-400" />
                  ) : (
                    <WifiOffIcon className="size-3.5 text-white/70" />
                  )}
                  <span>Low-Bandwidth Mode</span>
                </div>
                <div className="size-4 rounded-full border border-white/30 flex items-center justify-center">
                  {dataSaverMode && <div className="size-2 rounded-full bg-emerald-400" />}
                </div>
              </button>
            </div>
          )}

          {/* Quality Submenu */}
          {settingsSubmenu === "quality" && (
            <div className="space-y-1">
              <button
                type="button"
                onClick={() => setSettingsSubmenu("root")}
                className="px-2 py-1 text-[11px] font-semibold text-white/60 hover:text-white flex items-center gap-1 mb-1"
              >
                <ChevronRightIcon className="size-3.5 rotate-180" /> Back
              </button>

              {/* Auto Option */}
              <button
                type="button"
                onClick={() => handleQualitySelect(-1)}
                className="w-full flex items-center justify-between px-3 py-1.5 rounded-lg hover:bg-white/10 transition-colors text-start font-mono"
              >
                <div className="flex items-center gap-2">
                  {selectedQuality === -1 && <CheckIcon className="size-3.5 text-primary" />}
                  <span className={cn(selectedQuality === -1 && "font-bold text-primary")}>
                    Auto
                  </span>
                </div>
                {activeQualityIndex >= 0 && hlsRef.current?.levels?.[activeQualityIndex] && (
                  <span className="text-[10px] text-white/50">
                    ({hlsRef.current.levels[activeQualityIndex].height}p)
                  </span>
                )}
              </button>

              {/* Discrete HLS Quality Levels */}
              {qualityLevels.map((lvl) => {
                const isSelected = selectedQuality === lvl.index;
                return (
                  <button
                    key={lvl.index}
                    type="button"
                    onClick={() => handleQualitySelect(lvl.index)}
                    className="w-full flex items-center justify-between px-3 py-1.5 rounded-lg hover:bg-white/10 transition-colors text-start font-mono"
                  >
                    <div className="flex items-center gap-2">
                      {isSelected && <CheckIcon className="size-3.5 text-primary" />}
                      <span className={cn(isSelected && "font-bold text-primary")}>
                        {lvl.label}
                      </span>
                    </div>
                    {lvl.height >= 720 && (
                      <span className="text-[9px] px-1 rounded bg-primary/20 text-primary font-bold">
                        HD
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          )}

          {/* Speed Submenu */}
          {settingsSubmenu === "speed" && (
            <div className="space-y-1">
              <button
                type="button"
                onClick={() => setSettingsSubmenu("root")}
                className="px-2 py-1 text-[11px] font-semibold text-white/60 hover:text-white flex items-center gap-1 mb-1"
              >
                <ChevronRightIcon className="size-3.5 rotate-180" /> Back
              </button>

              {PLAYBACK_RATES.map((rate) => {
                const isSelected = playbackRate === rate;
                return (
                  <button
                    key={rate}
                    type="button"
                    onClick={() => handleRateChange(rate)}
                    className="w-full flex items-center justify-between px-3 py-1.5 rounded-lg hover:bg-white/10 transition-colors text-start font-mono"
                  >
                    <div className="flex items-center gap-2">
                      {isSelected && <CheckIcon className="size-3.5 text-primary" />}
                      <span className={cn(isSelected && "font-bold text-primary")}>
                        {rate === 1 ? "Normal (1.0x)" : `${rate}x`}
                      </span>
                    </div>
                  </button>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* 8. Bottom Control Bar Overlay (z-30) */}
      <div
        className={cn(
          "absolute bottom-0 inset-x-0 bg-gradient-to-t from-black/95 via-black/60 to-transparent pt-8 pb-3 px-4 transition-opacity duration-300 z-30",
          showControls || !isPlaying || isSettingsOpen ? "opacity-100" : "opacity-0 pointer-events-none"
        )}
      >
        {/* Interactive Progress Scrubber */}
        <div
          ref={timelineRef}
          onMouseMove={handleTimelineMove}
          onMouseDown={handleTimelineMouseDown}
          onMouseUp={handleTimelineMouseUp}
          onMouseLeave={() => setHoverPosition(null)}
          className="group/timeline relative h-2 hover:h-3 w-full rounded-full bg-white/20 cursor-pointer transition-all mb-3 flex items-center"
        >
          {/* Buffered Track */}
          <div
            className="absolute left-0 top-0 bottom-0 rounded-full bg-white/35 transition-all"
            style={{ width: `${bufferedPct}%` }}
          />

          {/* Played Track */}
          <div
            className="absolute left-0 top-0 bottom-0 rounded-full bg-primary transition-all flex items-center justify-end"
            style={{ width: `${duration > 0 ? (currentTime / duration) * 100 : 0}%` }}
          >
            {/* Scrubber Head Handle */}
            <div className="size-3.5 rounded-full bg-white shadow-md scale-0 group-hover/timeline:scale-100 transition-transform" />
          </div>

          {/* Hover Timecode Tooltip Pill */}
          {hoverPosition !== null && (
            <div
              className="absolute -top-7 px-2 py-0.5 rounded-md bg-black/90 text-white font-mono text-[10px] -translate-x-1/2 pointer-events-none shadow-md border border-white/20"
              style={{ left: `${hoverPosition}%` }}
            >
              {formatTime(hoverTime)}
            </div>
          )}
        </div>

        {/* Action Controls Row */}
        <div className="flex items-center justify-between gap-3 text-white">
          {/* Left Controls: Play, Rewind, Fast Forward, Volume, Time */}
          <div className="flex items-center gap-1 sm:gap-2">
            {/* Play/Pause Button */}
            <button
              type="button"
              onClick={handlePlayPause}
              className="p-2 rounded-xl hover:bg-white/15 transition-colors cursor-pointer"
              title={isPlaying ? "Pause (Space/k)" : "Play (Space/k)"}
            >
              {isPlaying ? (
                <PauseIcon className="size-5 fill-white" />
              ) : (
                <PlayIcon className="size-5 fill-white translate-x-0.5" />
              )}
            </button>

            {/* Rewind 10s */}
            <button
              type="button"
              onClick={() => handleSeek(-10)}
              className="p-1.5 rounded-xl hover:bg-white/15 transition-colors text-white/80 hover:text-white cursor-pointer"
              title="Rewind 10s (j)"
            >
              <RotateCcwIcon className="size-4" />
            </button>

            {/* Fast Forward 10s */}
            <button
              type="button"
              onClick={() => handleSeek(10)}
              className="p-1.5 rounded-xl hover:bg-white/15 transition-colors text-white/80 hover:text-white cursor-pointer"
              title="Forward 10s (l)"
            >
              <RotateCwIcon className="size-4" />
            </button>

            {/* Volume Control Group with Expandable Slider */}
            <div className="group/volume flex items-center gap-1 relative pl-1">
              <button
                type="button"
                onClick={handleToggleMute}
                className="p-1.5 rounded-xl hover:bg-white/15 transition-colors cursor-pointer"
                title={isMuted ? "Unmute (m)" : "Mute (m)"}
              >
                {isMuted || volume === 0 ? (
                  <VolumeXIcon className="size-4.5 text-destructive" />
                ) : volume < 0.5 ? (
                  <Volume1Icon className="size-4.5" />
                ) : (
                  <Volume2Icon className="size-4.5" />
                )}
              </button>

              <div className="w-0 group-hover/volume:w-20 overflow-hidden transition-all duration-200 flex items-center">
                <input
                  type="range"
                  min="0"
                  max="1"
                  step="0.05"
                  value={isMuted ? 0 : volume}
                  onChange={(e) => handleVolumeChange(parseFloat(e.target.value))}
                  className="w-18 h-1 bg-white/30 accent-primary rounded-lg cursor-pointer"
                />
              </div>
            </div>

            {/* Live Time Display */}
            <div className="font-mono text-xs text-white/80 ml-2 tracking-tight">
              <span>{formatTime(currentTime)}</span>
              <span className="text-white/40 mx-1">/</span>
              <span>{formatTime(duration)}</span>
            </div>
          </div>

          {/* Right Controls: Quality Badge, Settings, Theater, Fullscreen */}
          <div className="flex items-center gap-1 sm:gap-2">
            {/* Quick Resolution Pill */}
            <button
              type="button"
              onClick={() => {
                setIsSettingsOpen(true);
                setSettingsSubmenu("quality");
              }}
              className="hidden sm:flex items-center gap-1 px-2.5 py-1 rounded-lg bg-white/10 hover:bg-white/20 border border-white/20 font-mono text-[11px] font-semibold text-white/90 transition-colors cursor-pointer"
              title="Change Resolution"
            >
              <span>{getQualityBadgeLabel()}</span>
            </button>

            {/* Settings Gear Popover Button */}
            <button
              type="button"
              onClick={() => {
                setIsSettingsOpen((open) => !open);
                setSettingsSubmenu("root");
              }}
              className={cn(
                "p-2 rounded-xl hover:bg-white/15 transition-colors cursor-pointer",
                isSettingsOpen && "bg-white/20 text-primary"
              )}
              title="Settings (Resolution, Speed)"
            >
              <SettingsIcon className="size-4.5" />
            </button>

            {/* Theater Mode Toggle */}
            <button
              type="button"
              onClick={handleToggleTheater}
              className={cn(
                "hidden sm:flex p-2 rounded-xl hover:bg-white/15 transition-colors cursor-pointer",
                effectiveTheater && "text-primary"
              )}
              title={effectiveTheater ? "Exit Theater Mode (t)" : "Theater Mode (t)"}
            >
              <TvIcon className="size-4.5" />
            </button>

            {/* Fullscreen Button */}
            <button
              type="button"
              onClick={toggleFullscreen}
              className="p-2 rounded-xl hover:bg-white/15 transition-colors cursor-pointer"
              title={isFullscreen ? "Exit Fullscreen (f)" : "Fullscreen (f)"}
            >
              {isFullscreen ? (
                <Minimize2Icon className="size-4.5" />
              ) : (
                <Maximize2Icon className="size-4.5" />
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
