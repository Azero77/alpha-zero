import { z } from 'zod';

const configSchema = z.object({
  apiUrl: z.string().url().default('http://localhost:5001/api'),
  uploadApiUrl: z.string().url().default('http://localhost:5001/api/video-uploading'),
  streamingApiUrl: z.string().url().default('http://localhost:5001/api/video'),
  cdnUrl: z.string().url().optional(),
  tenantId: z.string().uuid().optional(),
});

export const config = configSchema.parse({
  apiUrl: import.meta.env.VITE_API_URL,
  uploadApiUrl: import.meta.env.VITE_UPLOAD_API_URL,
  streamingApiUrl: import.meta.env.VITE_STREAMING_API_URL,
  cdnUrl: import.meta.env.VITE_CDN_URL,
  tenantId: import.meta.env.VITE_TENANT_ID,
});

export type Config = z.infer<typeof configSchema>;
