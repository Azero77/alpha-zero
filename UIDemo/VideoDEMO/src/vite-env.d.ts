/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL: string;
  readonly VITE_UPLOAD_API_URL: string;
  readonly VITE_STREAMING_API_URL: string;
  readonly VITE_CDN_URL?: string;
  readonly VITE_TENANT_ID?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
