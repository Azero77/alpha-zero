"use client";

import { useEffect, useRef, useState } from "react";
import { DynamicVisibleWatermark, type WatermarkData } from "./dynamic-visible-watermark";

export interface VideoPlayerProps {
  manifestUrl: string;
  watermarkContext: WatermarkData;
  poster?: string;
  autoPlay?: boolean;
  onEnded?: () => void;
  onTimeUpdate?: (currentTime: number, duration: number) => void;
  className?: string;
}

// Minimal Hls.js interface declaration for resilient dynamic loading without build-time bundle hard-dependency
interface HlsInstance {
  loadSource(url: string): void;
  attachMedia(media: HTMLMediaElement): void;
  destroy(): void;
  on(event: string, callback: (...args: any[]) => void): void;
  recoverMediaError(): void;
  startLoad(): void;
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

/**
 * Enterprise HLS Video Player component with:
 * - Credentialed requests (withCredentials: true) for signed capability cookies
 * - Ephemeral key recovery handling (410 Expired Ticket)
 * - Anti-leak drifting canvas watermark overlay
 * - Native iOS/Safari HLS playback support
 */
export function VideoPlayer({
  manifestUrl,
  watermarkContext,
  poster,
  autoPlay = false,
  onEnded,
  onTimeUpdate,
  className = "",
}: VideoPlayerProps) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const hlsRef = useRef<HlsInstance | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const video = videoRef.current;
    if (!video || !manifestUrl) return;

    setError(null);
    setIsLoading(true);

    const initPlayer = () => {
      // 1. Native HLS support (Safari on macOS and iOS devices)
      if (video.canPlayType("application/vnd.apple.mpegurl")) {
        video.src = manifestUrl;
        setIsLoading(false);
        return;
      }

      // 2. MediaSource Extensions via Hls.js
      if (typeof window !== "undefined" && window.Hls?.isSupported()) {
        const Hls = window.Hls;
        const hls = new Hls({
          // Send cookies with manifest, playlist, key, and segment requests
          xhrSetup: (xhr: XMLHttpRequest) => {
            xhr.withCredentials = true;
          },
          // Low-bandwidth tuning
          maxBufferLength: 30,
          maxMaxBufferLength: 60,
          maxBufferSize: 30 * 1000 * 1000, // 30MB
          enableWorker: true,
          lowLatencyMode: false,
        });

        hlsRef.current = hls;

        hls.loadSource(manifestUrl);
        hls.attachMedia(video);

        hls.on(Hls.Events.MANIFEST_PARSED, () => {
          setIsLoading(false);
          if (autoPlay) {
            video.play().catch(() => {
              // Browser autoplay policy prevented playback
            });
          }
        });

        hls.on(Hls.Events.ERROR, (_event: string, data: any) => {
          if (data.fatal) {
            switch (data.type) {
              case Hls.ErrorTypes.NETWORK_ERROR:
                // Handle 410 Key Ticket Expiry: reload playlist to mint fresh ticket
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
                setError("Playback error: Unable to load video.");
                break;
            }
          }
        });
      } else {
        // Fallback for unsupported browsers
        video.src = manifestUrl;
        setIsLoading(false);
      }
    };

    // Dynamically load Hls.js script if not present
    if (typeof window !== "undefined" && !window.Hls && !video.canPlayType("application/vnd.apple.mpegurl")) {
      const script = document.createElement("script");
      script.src = "https://cdn.jsdelivr.net/npm/hls.js@1.5.17/dist/hls.min.js";
      script.async = true;
      script.onload = () => initPlayer();
      script.onerror = () => {
        setError("Failed to load video streaming engine.");
        setIsLoading(false);
      };
      document.body.appendChild(script);

      return () => {
        if (script.parentNode) {
          script.parentNode.removeChild(script);
        }
      };
    } else {
      initPlayer();
    }

    return () => {
      if (hlsRef.current) {
        hlsRef.current.destroy();
        hlsRef.current = null;
      }
    };
  }, [manifestUrl, autoPlay]);

  return (
    <div className={`group relative aspect-video w-full overflow-hidden rounded-xl bg-black ${className}`}>
      <video
        ref={videoRef}
        poster={poster}
        controls
        playsInline
        className="h-full w-full object-contain"
        onEnded={onEnded}
        onTimeUpdate={() => {
          if (videoRef.current && onTimeUpdate) {
            onTimeUpdate(videoRef.current.currentTime, videoRef.current.duration);
          }
        }}
      />

      {/* Floating Dynamic Visible Watermark Layer */}
      {watermarkContext && <DynamicVisibleWatermark data={watermarkContext} />}

      {/* Loading Overlay */}
      {isLoading && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/60 backdrop-blur-sm z-20 pointer-events-none">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-white/20 border-t-white" />
        </div>
      )}

      {/* Error Overlay */}
      {error && (
        <div className="absolute inset-0 flex flex-col items-center justify-center bg-black/90 p-4 text-center z-40">
          <p className="text-sm font-medium text-red-400">{error}</p>
          <button
            type="button"
            onClick={() => window.location.reload()}
            className="mt-3 rounded-md bg-white/10 px-3 py-1.5 text-xs font-semibold text-white hover:bg-white/20"
          >
            Retry Playback
          </button>
        </div>
      )}
    </div>
  );
}
