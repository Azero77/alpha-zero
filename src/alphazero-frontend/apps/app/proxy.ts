import {
  noseconeOptions,
  noseconeOptionsWithToolbar,
  securityMiddleware,
} from "@repo/security/proxy";
import { resolveTenantSubdomain } from "@repo/design-system/lib/tenant-utils";
import { type NextProxy, type NextRequest, NextResponse } from "next/server";
import { env } from "./env";

export { resolveTenantSubdomain };

const securityHeaders = env.FLAGS_SECRET
  ? securityMiddleware(noseconeOptionsWithToolbar)
  : securityMiddleware(noseconeOptions);

export default (async (request: NextRequest) => {
  const subdomain = resolveTenantSubdomain(request.headers, request.nextUrl.searchParams);

  const requestHeaders = new Headers(request.headers);
  if (subdomain) {
    requestHeaders.set("x-tenant-subdomain", subdomain);
  }

  const response = NextResponse.next({
    request: {
      headers: requestHeaders,
    },
  });

  try {
    const secResponse = await (securityHeaders as any)(request);
    if (secResponse?.headers) {
      secResponse.headers.forEach((value: string, key: string) => {
        response.headers.set(key, value);
      });
    }
  } catch {
    // Fallback if security middleware does not modify response
  }

  return response;
}) as unknown as NextProxy;

export const config = {
  matcher: [
    // Skip Next.js internals and all static files, unless found in search params
    "/((?!_next|[^?]*\\.(?:html?|css|js(?!on)|jpe?g|webp|png|gif|svg|ttf|woff2?|ico|csv|docx?|xlsx?|zip|webmanifest)).*)",
    // Always run for API routes
    "/(api|trpc)(.*)",
  ],
};
