import axios from 'axios';

export const uploadToS3 = async (url: string, file: File, onProgress?: (percent: number) => void): Promise<void> => {
  await axios.put(url, file, {
    headers: {
      'Content-Type': file.type,
    },
    onUploadProgress: (progressEvent) => {
      const percentCompleted = Math.round((progressEvent.loaded * 100) / (progressEvent.total || 1));
      if (onProgress) onProgress(percentCompleted);
    },
  });
};
