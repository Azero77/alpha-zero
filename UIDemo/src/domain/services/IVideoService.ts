import type { PagedResult, UploadRequest, UploadResponse, Video, StreamingInfo } from '../models/video';

export interface IVideoService {
  getVideos(page?: number, perPage?: number): Promise<PagedResult<Video>>;
  getVideo(id: string): Promise<Video>;
  getStreamingInfo(id: string): Promise<StreamingInfo>;
  deleteVideo(id: string): Promise<void>;
  requestUpload(request: UploadRequest): Promise<UploadResponse>;
  uploadFile(url: string, file: File, metadata: { fileName: string, videoId: string, tenantId: string }, onProgress?: (percent: number) => void): Promise<void>;
}
