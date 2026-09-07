"use server";

import { type ApiClient, createApi } from "./client";

export function createServerApi(options: {
  baseUrl: string;
  getTenantId?: () => string | null | Promise<string | null>;
  getToken?: () => string | null | Promise<string | null>;
}): ApiClient {
  return createApi({
    baseUrl: options.baseUrl,
    getTenantId: options.getTenantId,
    getToken: options.getToken,
  });
}
