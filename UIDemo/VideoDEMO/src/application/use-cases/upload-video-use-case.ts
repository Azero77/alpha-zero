import type { IVideoRepository, UploadResponse } from '../../domain/repositories/video-repository';
import { config } from '../../core/config';

export class UploadVideoUseCase {
  private readonly repository: IVideoRepository;

  constructor(repository: IVideoRepository) {
    this.repository = repository;
  }

  async execute(
    file: File,
    title: string,
    description?: string,
    onProgress?: (progress: number) => void,
    options?: {
      transcodingMethod?: string;
      encryptionMethod?: string;
      targetResourceArn?: string;
      thumbnailFile?: File;
    }
  ): Promise<UploadResponse> {
    // Step 1: Request presigned URL(s) from backend
    const uploadInfo = await this.repository.requestUpload({
      fileName: file.name,
      contentType: file.type || 'video/mp4',
      title,
      description,
      transcodingMethod: options?.transcodingMethod ?? 'FFMPEG',
      encryptionMethod: options?.encryptionMethod ?? 'None',
      targetResourceArn:
        options?.targetResourceArn ??
        `az:video:${config.tenantId}:video/00000000-0000-0000-0000-000000000001`,
      thumbnailFileName: options?.thumbnailFile?.name,
      thumbnailContentType: options?.thumbnailFile?.type,
    });

    // Step 2: Upload video to S3 using backend-returned headers
    await this.repository.uploadToS3(
      uploadInfo.preSignedUrl,
      file,
      uploadInfo.headers,
      onProgress
    );

    // Step 3: Upload thumbnail to S3 if provided and presigned URL was returned
    if (
      options?.thumbnailFile &&
      uploadInfo.thumbnailPreSignedUrl &&
      uploadInfo.thumbnailHeaders
    ) {
      await this.repository.uploadToS3(
        uploadInfo.thumbnailPreSignedUrl,
        options.thumbnailFile,
        uploadInfo.thumbnailHeaders
      );
    }

    return uploadInfo;
  }
}
