import axios from 'axios';
import { config } from '../config';

const apiClient = axios.create({
  baseURL: config.apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});

export interface UploadResponse {
  videoId: string;
  key: string;
  preSignedUrl: string;
}

export const getPresignedUrl = async (fileName: string, contentType: string): Promise<UploadResponse> => {
  // Matches the endpoint: POST api/courses/upload
  // and request: { fileName, contentType }
  const response = await apiClient.post<UploadResponse>('/video-uploading/upload', {
    fileName,
    contentType,
  });
  return response.data;
};
