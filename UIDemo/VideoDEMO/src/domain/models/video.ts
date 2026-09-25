export type VideoStatus = 'Processing' | 'Published' | 'Failed' | 'Deleted';

export interface Resolution {
  width: number;
  height: number;
}

export interface VideoMetadata {
  originalFileName: string;
  contentType: string;
  fileSize: number;
  transcodingMethod: string;
  encryptionMethod?: string;
}

export interface VideoSpecifications {
  duration: string; // ISO 8601 or TimeSpan string e.g. "00:05:30"
  resolution: Resolution;
}

export interface Video {
  id: string;
  tenantId: string;
  title: string;
  description?: string;
  status: VideoStatus;
  thumbnailUrl?: string;
  streamingUrl?: string;
  sagaState?: string;
  metadata: VideoMetadata;
  specifications: VideoSpecifications;
  sourceKey: string;
  outputFolder?: string;
  createdOn: string;
  publishedOn?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  currentPage: number;
  pageSize: number;
}
