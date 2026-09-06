import ky, { type HTTPError } from "ky";
import {
  ApiErrorException,
  ApiErrorResponseSchema,
  type ApiErrorResponse,
} from "@repo/lms-types";

/**
 * Creates a configured Ky client for talking to the AlphaZero ASP.NET Core API.
 */
export function createLmsClient(options?: {
  baseUrl?: string;
  getAccessToken?: () => Promise<string | null | undefined> | string | null | undefined;
}) {
  const prefixUrl =
    options?.baseUrl ||
    process.env.INTERNAL_CSHARP_API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://localhost:5000";

  return ky.create({
    prefixUrl,
    timeout: 15_000,
    retry: {
      limit: 2,
      methods: ["get"],
      statusCodes: [408, 502, 503, 504],
    },
    hooks: {
      beforeRequest: [
        async (request) => {
          if (options?.getAccessToken) {
            const token = await options.getAccessToken();
            if (token) {
              request.headers.set("Authorization", `Bearer ${token}`);
            }
          }
        },
      ],
      beforeError: [
        async (error: HTTPError) => {
          const { response } = error;
          const contentType = response?.headers.get("content-type");

          if (contentType?.includes("json")) {
            try {
              const rawJson = await response.json();
              const parsed = ApiErrorResponseSchema.safeParse(rawJson);

              if (parsed.success) {
                // Throw typed ApiErrorException matching UnifiedErrorBody
                return new ApiErrorException(parsed.data) as unknown as HTTPError;
              }
            } catch {
              // Ignore body parsing errors and let standard HTTPError propagate
            }
          }

          return error;
        },
      ],
    },
  });
}

export const api = createLmsClient();
