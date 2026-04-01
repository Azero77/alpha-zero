import type { IVideoRepository, UploadResponse } from '../../domain/repositories/video-repository';

export class UploadVideoUseCase {
  private videoRepository: IVideoRepository;

  constructor(videoRepository: IVideoRepository) {
    this.videoRepository = videoRepository;
  }

  async execute(file: File, onProgress?: (progress: number) => void): Promise<UploadResponse> {
    // 1. Request presigned URL from backend
    const uploadInfo = await this.videoRepository.requestUpload({
      fileName: file.name,
      contentType: file.type || 'video/mp4',
    });

    // 2. Upload file directly to S3
    await this.videoRepository.uploadToS3(
      uploadInfo.preSignedUrl, 
      file, 
      uploadInfo.videoId, 
      uploadInfo.tenantId, 
      onProgress
    );

    return uploadInfo;
  }
}
