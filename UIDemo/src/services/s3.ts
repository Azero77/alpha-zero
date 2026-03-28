import axios from 'axios';

export const uploadToS3 = async (url: string, file: File, fileName: string, videoId: string, tenantId: string, onProgress?: (percent: number) => void): Promise<void> => {
  await axios.put(url, file, {
    headers: {
      'Content-Type': file.type,
      'x-amz-meta-file-name': fileName,
      'x-amz-meta-VideoId': videoId,
      'x-amz-meta-TenantId': tenantId
    },
    onUploadProgress: (progressEvent) => {
      const percentCompleted = Math.round((progressEvent.loaded * 100) / (progressEvent.total || 1));
      if (onProgress) onProgress(percentCompleted);
    },
  });
};
