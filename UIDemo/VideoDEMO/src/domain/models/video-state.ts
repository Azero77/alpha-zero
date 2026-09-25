/**
 * Represents the MassTransit saga state for a video processing pipeline.
 * Maps to the backend VideoState entity in Infrastructure/Sagas/VideoState.cs
 */
export interface VideoState {
  correlationId: string;
  tenantId: string;
  currentState: string;
  mediaConverterJobId?: string;
  key?: string;
  sourceWidth?: number;
  sourceHeight?: number;
  duration?: string;
  s3OutputPrefix?: string;
  finalUrl?: string;
  encryptionMethod?: string;
  customThumbnailKey?: string;
  targetResourceArn?: string;
  isFailed: boolean;
  version: number;
}

/**
 * Real-time progress update received via SignalR VideoProgressHub.
 * Maps to VideoProgressNotification in Application/Services/IVideoProgressNotifier.cs
 */
export interface VideoProgressUpdate {
  videoId: string;
  tenantId: string;
  stage: 'uploading' | 'analyzing' | 'preparing' | 'transcoding' | 'publishing' | string;
  status: 'IN_PROGRESS' | 'COMPLETE' | 'FAILED' | string;
  percentage: number | null;
  metadata: string | null;
}
