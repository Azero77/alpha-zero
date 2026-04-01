export type VideoStatus = 'Pending' | 'Analyzing' | 'Transcoding' | 'Distributing' | 'Published' | 'Failed';

export interface Resolution {
  width: number;
  height: number;
}

export interface VideoMetadata {
  originalFileName: string;
  contentType: string;
  fileSize: number;
}

export interface VideoSpecifications {
  duration: string; // ISO 8601 or TimeSpan string
  resolution: Resolution;
}

export interface Video {
  id: string;
  tenantId: string;
  title: string;
  description?: string;
  status: VideoStatus;
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
  page: number;
  perPage: number;
}
