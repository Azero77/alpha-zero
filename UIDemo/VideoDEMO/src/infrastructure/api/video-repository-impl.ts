import axios from 'axios';
import type {
  IVideoRepository,
  UploadRequest,
  UploadResponse,
  StreamingInfo,
  UpdateVideoInfoRequest,
} from '../../domain/repositories/video-repository';
import type { Video, PagedResult } from '../../domain/models/video';
import type { VideoState } from '../../domain/models/video-state';
import { apiClient } from './api-client';
import { config } from '../../core/config';

export class VideoRepositoryImpl implements IVideoRepository {

  // ── List & Get ──────────────────────────────────────────────

  async getVideos(page: number, perPage: number): Promise<PagedResult<Video>> {
    const response = await apiClient.get<PagedResult<Video>>(
      `${config.uploadApiUrl}/debug/videos`,
      { params: { page, perPage } }
    );
    return response.data;
  }

  async getVideoById(id: string): Promise<Video> {
    const response = await apiClient.get<Video>(
      `${config.uploadApiUrl}/debug/videos/${id}`
    );
    return response.data;
  }

  async getVideoState(id: string): Promise<VideoState> {
    const response = await apiClient.get<VideoState>(
      `${config.uploadApiUrl}/debug/videos/${id}/state`
    );
    return response.data;
  }

  // ── Upload ──────────────────────────────────────────────────

  async requestUpload(request: UploadRequest): Promise<UploadResponse> {
    const response = await apiClient.post<UploadResponse>(
      `${config.uploadApiUrl}/upload`,
      {
        fileName: request.fileName,
        contentType: request.contentType,
        title: request.title,
        description: request.description,
        transcodingMethod: request.transcodingMethod ?? 'FFMPEG',
        encryptionMethod: request.encryptionMethod ?? 'None',
        targetResourceArn: request.targetResourceArn,
        // Optional custom thumbnail
        ...(request.thumbnailFileName && request.thumbnailContentType
          ? {
              ThumbnailFileName: request.thumbnailFileName,
              ThumbnailContentType: request.thumbnailContentType,
            }
          : {}),
      }
    );
    return response.data;
  }

  /**
   * Upload a file directly to S3 using the presigned URL and headers
   * returned by the backend. Do NOT hardcode x-amz-meta headers — 
   * use the exact headers dict from the UploadResponse.
   */
  async uploadToS3(
    url: string,
    file: File,
    headers: Record<string, string>,
    onProgress?: (progress: number) => void
  ): Promise<void> {
    await axios.put(url, file, {
      headers,
      onUploadProgress: (progressEvent) => {
        if (onProgress && progressEvent.total) {
          const progress = Math.round(
            (progressEvent.loaded * 100) / progressEvent.total
          );
          onProgress(progress);
        }
      },
    });
  }

  // ── Mutations ───────────────────────────────────────────────

  async updateVideoInfo(
    id: string,
    request: UpdateVideoInfoRequest
  ): Promise<void> {
    await apiClient.patch(`${config.uploadApiUrl}/debug/videos/${id}`, request);
  }

  async deleteVideo(id: string): Promise<void> {
    await apiClient.delete(`${config.uploadApiUrl}/debug/videos/${id}`);
  }

  // ── Streaming ───────────────────────────────────────────────

  async getStreamingInfo(id: string): Promise<StreamingInfo> {
    const response = await apiClient.get<StreamingInfo>(
      `${config.streamingApiUrl}/${id}`
    );
    return response.data;
  }
}
