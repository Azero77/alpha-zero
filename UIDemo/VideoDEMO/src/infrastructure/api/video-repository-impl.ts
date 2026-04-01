import axios from 'axios';
import type { IVideoRepository, UploadRequest, UploadResponse, StreamingInfo } from '../../domain/repositories/video-repository';
import type { Video, PagedResult } from '../../domain/models/video';
import type { VideoState } from '../../domain/models/video-state';
import { apiClient } from './api-client';
import { config } from '../../core/config';

export class VideoRepositoryImpl implements IVideoRepository {
  async getVideos(page: number, perPage: number): Promise<PagedResult<Video>> {
    const response = await apiClient.get<PagedResult<Video>>(`${config.uploadApiUrl}/debug/videos`, {
      params: { page, perPage },
    });
    return response.data;
  }

  async getVideoById(id: string): Promise<Video> {
    const response = await apiClient.get<Video>(`${config.uploadApiUrl}/debug/videos/${id}`);
    return response.data;
  }

  async getVideoState(id: string): Promise<VideoState> {
    const response = await apiClient.get<VideoState>(`${config.uploadApiUrl}/debug/videos/${id}/state`);
    return response.data;
  }

  async deleteVideo(id: string): Promise<void> {
    await apiClient.delete(`${config.uploadApiUrl}/debug/videos/${id}`);
  }

  async requestUpload(request: UploadRequest): Promise<UploadResponse> {
    const response = await apiClient.post<UploadResponse>(`${config.uploadApiUrl}/upload`, request);
    return response.data;
  }

  async getStreamingInfo(id: string): Promise<StreamingInfo> {
    const response = await apiClient.get<StreamingInfo>(`${config.streamingApiUrl}/${id}`);
    return response.data;
  }

  async uploadToS3(url: string, file: File, videoId: string, tenantId: string, onProgress?: (progress: number) => void): Promise<void> {
    await axios.put(url, file, {
      headers: {
        'Content-Type': file.type,
        'x-amz-meta-file-name': file.name,
        'x-amz-meta-videoid': videoId,
        'x-amz-meta-tenantid': tenantId,
      },
      onUploadProgress: (progressEvent) => {
        if (onProgress && progressEvent.total) {
          const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
          onProgress(progress);
        }
      },
    });
  }
}
