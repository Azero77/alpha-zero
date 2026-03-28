export const VideoStatus = {
  Processing: 0,
  Published: 1,
  Failed: 2,
  Deleted: 3,
} as const;

export type VideoStatus = typeof VideoStatus[keyof typeof VideoStatus];

export interface VideoResolution {
  width: number;
  height: number;
}

export interface VideoSpecifications {
  duration: string;
  resolution: VideoResolution;
}

export interface VideoMetadata {
  originalFileName: string;
  contentType: string;
  fileSize: number;
}

export interface Video {
  id: string;
  tenantId: string;
  title: string;
  description: string | null;
  status: VideoStatus;
  metadata: VideoMetadata;
  specifications: VideoSpecifications | null;
  sourceKey: string;
  outputFolder: string | null;
  createdOn: string;
  publishedOn: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  perPage: number;
}

export interface UploadRequest {
  fileName: string;
  contentType: string;
}

export interface UploadResponse {
  videoId: string;
  tenantId: string;
  key: string;
  preSignedUrl: string;
}

export interface StreamingInfo {
  manifestUrl: string;
  keyId: string | null;
  key: string | null;
}
