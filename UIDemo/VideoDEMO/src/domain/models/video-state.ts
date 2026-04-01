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
  isFailed: boolean;
  version: number;
}
