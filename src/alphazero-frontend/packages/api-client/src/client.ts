import type { Middleware } from "openapi-fetch";
import createClient from "openapi-fetch";
import {
  ApiErrorException,
  ApiErrorResponseSchema,
  type paths,
} from "../../lms-types/src";

export const errorHandlerMiddlware: Middleware = {
  async onResponse({ response }) {
    if (response.ok) {
      return undefined;
    }
    const cloned = response.clone();
    const contentType = cloned.headers.get("content-type") ?? "";
    if (
      contentType.includes("application/problem+json") ||
      contentType.includes("application/json")
    ) {
      try {
        const body = await cloned.json();
        const parsed = ApiErrorResponseSchema.safeParse(body);
        if (parsed.success) {
          throw new ApiErrorException(parsed.data);
        }
      } catch (error) {
        if (error instanceof ApiErrorException) {
          throw error;
        }
      }
    }

    throw new ApiErrorException({
      status: response.status,
      title: response.statusText || "Request Failed",
      errors: [],
    });
  },
};

export const createTenantMiddleware = (
  getTenantId: () => string | null | Promise<string | null>
): Middleware => {
  return {
    async onRequest({ request }) {
      //here we are reading the tenant id from the token , if it was guid.empty we will check the request header for X-TenantId
      const tenantId = await getTenantId();
      if (tenantId) {
        request.headers.set("X-TenantId", tenantId);
      }
      return request;
    },
  };
};

export const createAuthMiddleware = (
  getToken: () => string | null | Promise<string | null>
): Middleware => {
  return {
    async onRequest({ request }) {
      const token = await getToken();
      if (token) {
        request.headers.set("Authorization", `Bearer ${token}`);
      }
      return request;
    },
  };
};

const RETRYABLE_STATUS_CODES = new Set([502, 503, 504]);
const MAX_RETRIES = 3;
const BASE_DELAY_MS = 500;
export const retryMiddleware: Middleware = {
  async onResponse({ response, request }) {
    if (response.ok) {
      return undefined;
    }
    if (!RETRYABLE_STATUS_CODES.has(response.status)) {
      return undefined;
    }

    // Extract retry count from a custom header we set
    const attempt = Number(request.headers.get("x-retry-attempt") || "0");
    if (attempt >= MAX_RETRIES) {
      return undefined;
    }

    // Exponential backoff with jitter
    const delay = BASE_DELAY_MS * 2 ** attempt + Math.random() * 100;
    await new Promise((resolve) => setTimeout(resolve, delay));

    // Clone request and retry
    const retryRequest = request.clone();
    retryRequest.headers.set("x-retry-attempt", String(attempt + 1));

    return fetch(retryRequest);
  },
};

export interface createApiOptions {
  baseUrl: string;
  getTenantId?: () => string | null | Promise<string | null>;
  getToken?: () => string | null | Promise<string | null>;
}

export function createApi({
  baseUrl,
  getTenantId,
  getToken,
}: createApiOptions) {
  const api = createClient<paths>({
    baseUrl,
    headers: {
      "Content-Type": "application/json",
    },
  });

  if (getToken) {
    api.use(createAuthMiddleware(getToken));
  }
  if (getTenantId) {
    api.use(createTenantMiddleware(getTenantId));
  }

  api.use(retryMiddleware);
  api.use(errorHandlerMiddlware);
  return api;
}

export type ApiClient = ReturnType<typeof createApi>;
