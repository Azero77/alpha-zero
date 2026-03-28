import axios from 'axios';
import type { IVideoService } from '../../domain/services/IVideoService';
import type { PagedResult, UploadRequest, UploadResponse, Video, StreamingInfo } from '../../domain/models/video';
import { config } from '../../config';

const apiClient = axios.create({
  baseURL: config.apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});

export class VideoService implements IVideoService {
  async getVideos(page: number = 1, perPage: number = 10): Promise<PagedResult<Video>> {
    const response = await apiClient.get<PagedResult<Video>>('/video-uploading/debug/videos', {
      params: { page, perPage },
    });
    return response.data;
  }

  async getVideo(id: string): Promise<Video> {
    const response = await apiClient.get<Video>(`/video-uploading/debug/videos/${id}`);
    return response.data;
  }

  async getStreamingInfo(id: string): Promise<StreamingInfo> {
    const response = await apiClient.get<StreamingInfo>(`/video-uploading/debug/videos/${id}/streaming`);
    return response.data;
  }

  async deleteVideo(id: string): Promise<void> {
    await apiClient.delete(`/video-uploading/debug/videos/${id}`);
  }

  async requestUpload(request: UploadRequest): Promise<UploadResponse> {
    const response = await apiClient.post<UploadResponse>('/video-uploading/upload', request);
    return response.data;
  }

  async uploadFile(
    url: string,
    file: File,
    metadata: { fileName: string; videoId: string; tenantId: string },
    onProgress?: (percent: number) => void
  ): Promise<void> {
    await axios.put(url, file, {
      headers: {
        'Content-Type': file.type,
        'x-amz-meta-file-name': metadata.fileName,
        'x-amz-meta-VideoId': metadata.videoId,
        'x-amz-meta-TenantId': metadata.tenantId,
      },
      onUploadProgress: (progressEvent) => {
        const percentCompleted = Math.round((progressEvent.loaded * 100) / (progressEvent.total || 1));
        if (onProgress) onProgress(percentCompleted);
      },
    });
  }
}

export const videoService = new VideoService();
