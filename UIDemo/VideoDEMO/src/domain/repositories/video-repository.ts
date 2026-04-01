import type { Video, PagedResult } from '../models/video';
import type { VideoState } from '../models/video-state';

export interface UploadRequest {
  fileName: string;
  contentType: string;
}

export interface UploadResponse {
  videoId: string;
  tenantId: string;
  key: string;
  preSignedUrl: string;
}

export interface StreamingInfo {
  url: string;
  key: string;
}

export interface IVideoRepository {
  getVideos(page: number, perPage: number): Promise<PagedResult<Video>>;
  getVideoById(id: string): Promise<Video>;
  getVideoState(id: string): Promise<VideoState>;
  deleteVideo(id: string): Promise<void>;
  requestUpload(request: UploadRequest): Promise<UploadResponse>;
  getStreamingInfo(id: string): Promise<StreamingInfo>;
  uploadToS3(url: string, file: File, videoId: string, tenantId: string, onProgress?: (progress: number) => void): Promise<void>;
}
