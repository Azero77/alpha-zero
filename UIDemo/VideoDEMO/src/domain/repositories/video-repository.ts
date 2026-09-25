import type { Video, PagedResult } from '../models/video';
import type { VideoState } from '../models/video-state';

// ── Upload ───────────────────────────────────────────────────

export interface UploadRequest {
  fileName: string;
  contentType: string;
  title: string;
  description?: string;
  transcodingMethod?: string;   // "FFMPEG" | "MediaConvert"
  encryptionMethod?: string;    // "None" | "ClearKey"
  targetResourceArn: string;
  thumbnailFileName?: string;   // Optional custom thumbnail
  thumbnailContentType?: string;
}

export interface UploadResponse {
  videoId: string;
  tenantId: string;
  key: string;
  preSignedUrl: string;
  transcodingMethod: string;
  encryptionMethod: string;
  headers: Record<string, string>;
  thumbnailKey?: string;
  thumbnailPreSignedUrl?: string;
  thumbnailHeaders?: Record<string, string>;
}

// ── Streaming ────────────────────────────────────────────────

export interface StreamingInfo {
  url: string;
  encryptionMethod?: string;
  licenseUrl?: string;
  drm?: {
    widevineUrl?: string;
    playReadyUrl?: string;
    token?: string;
  };
}

// ── Video CRUD ───────────────────────────────────────────────

export interface UpdateVideoInfoRequest {
  title: string;
  description?: string;
}

// ── Repository Contract ──────────────────────────────────────

export interface IVideoRepository {
  // List & Get
  getVideos(page: number, perPage: number): Promise<PagedResult<Video>>;
  getVideoById(id: string): Promise<Video>;
  getVideoState(id: string): Promise<VideoState>;

  // Upload
  requestUpload(request: UploadRequest): Promise<UploadResponse>;
  uploadToS3(
    url: string,
    file: File,
    headers: Record<string, string>,
    onProgress?: (progress: number) => void
  ): Promise<void>;

  // Mutations
  updateVideoInfo(id: string, request: UpdateVideoInfoRequest): Promise<void>;
  deleteVideo(id: string): Promise<void>;

  // Streaming
  getStreamingInfo(id: string): Promise<StreamingInfo>;
}
