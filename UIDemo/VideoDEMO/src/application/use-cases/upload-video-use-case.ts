import type { IVideoRepository, UploadResponse } from '../../domain/repositories/video-repository';

export class UploadVideoUseCase {
  private videoRepository: IVideoRepository;

  constructor(videoRepository: IVideoRepository) {
    this.videoRepository = videoRepository;
  }

  async execute(file: File, title: string, description?: string, onProgress?: (progress: number) => void): Promise<UploadResponse> {
    // 1. Request presigned URL from backend
    const uploadInfo = await this.videoRepository.requestUpload({
      fileName: file.name,
      contentType: file.type || 'video/mp4',
      title,
      description
    });

    // 2. Upload file directly to S3
    await this.videoRepository.uploadToS3(
      uploadInfo.preSignedUrl, 
      file, 
      uploadInfo.videoId, 
      uploadInfo.tenantId, 
      title,
      description || '',
      uploadInfo.transcodingMethod,
      uploadInfo.encryptionMethod,
      onProgress
    );

    return uploadInfo;
  }
}
