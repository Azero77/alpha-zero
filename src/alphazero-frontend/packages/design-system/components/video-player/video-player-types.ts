import type { WatermarkData } from "./dynamic-visible-watermark";

export interface VideoQualityLevel {
  index: number;
  height: number;
  width?: number;
  bitrate: number;
  label: string;
}

export type PlaybackRate = 0.5 | 0.75 | 1 | 1.25 | 1.5 | 1.75 | 2;

export interface VideoPlayerProps {
  /** Manifest URL (.m3u8 for HLS or standard media file) */
  manifestUrl: string;
  /** Security & forensic watermark identity */
  watermarkContext: WatermarkData;
  /** Watermark opacity between 0.05 and 1.0 (default: 0.22) */
  watermarkOpacity?: number;
  /** Toggle watermark visibility */
  showWatermark?: boolean;
  /** Video poster image URL */
  poster?: string;
  /** Title displayed in the player top bar */
  title?: string;
  /** Auto play video on load */
  autoPlay?: boolean;
  /** Initial Syrian / Low-Bandwidth Data Saver mode (caps max resolution to 480p) */
  initialDataSaver?: boolean;
  /** External Theater mode control */
  isTheater?: boolean;
  /** Callback when user toggles theater mode */
  onTheaterToggle?: (isTheater: boolean) => void;
  /** Playback ended callback */
  onEnded?: () => void;
  /** Playback progress time update callback */
  onTimeUpdate?: (currentTime: number, duration: number) => void;
  /** Container CSS class names */
  className?: string;
}
