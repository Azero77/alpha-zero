import { z } from 'zod';

const configSchema = z.object({
  apiUrl: z.string().url().default('https://localhost:7016'),
  uploadApiUrl: z.string().url().default('https://localhost:7016/api/video-uploading'),
  streamingApiUrl: z.string().url().default('https://localhost:7016/api/video'),
  cdnUrl: z.string().url().optional(),
  tenantId: z.string().uuid().default('9ac6bf72-f911-452e-a43a-ae9b3e26238c'),
  signalRHubUrl: z.string().url().default('https://localhost:7016/hubs/video-progress'),
  authToken: z.string().optional(),
});

export const config = configSchema.parse({
  apiUrl: import.meta.env.VITE_API_URL,
  uploadApiUrl: import.meta.env.VITE_UPLOAD_API_URL,
  streamingApiUrl: import.meta.env.VITE_STREAMING_API_URL,
  cdnUrl: import.meta.env.VITE_CDN_URL,
  signalRHubUrl: import.meta.env.VITE_SIGNALR_HUB_URL,
  tenantId: import.meta.env.VITE_TENANT_ID,
  authToken: import.meta.env.VITE_AUTH_TOKEN,
});

export type Config = z.infer<typeof configSchema>;
