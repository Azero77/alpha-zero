/**
 * AlphaZero Cloudflare Edge Worker: Video Segment Delivery Authenticator
 * 
 * Intercepts media segment requests under /streaming/{tenantId}/{videoId}/*
 * Validates HMAC-SHA256 edge capability cookies (cf_video_token).
 * Injects X-Origin-Auth header before proxying to private AWS S3 origin.
 */

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    const origin = request.headers.get("Origin") || "*";

    // 1. CORS Preflight Handling (HLS.js withCredentials: true requirement)
    if (request.method === "OPTIONS") {
      return new Response(null, {
        status: 204,
        headers: {
          "Access-Control-Allow-Origin": origin,
          "Access-Control-Allow-Methods": "GET, HEAD, OPTIONS",
          "Access-Control-Allow-Headers": "Authorization, Range, X-Device-Id, X-Timestamp, X-Signature, Content-Type, Cookie",
          "Access-Control-Allow-Credentials": "true",
          "Access-Control-Max-Age": "86400",
        },
      });
    }

    // Only authenticate /streaming/ paths
    if (!url.pathname.startsWith("/streaming/")) {
      return fetch(request);
    }

    // 2. Extract Cookie
    const cookieHeader = request.headers.get("Cookie") || "";
    const cookies = Object.fromEntries(
      cookieHeader.split(";").map(c => {
        const [k, ...v] = c.trim().split("=");
        return [k, v.join("=")];
      })
    );

    const token = cookies["cf_video_token"];
    if (!token) {
      return new Response("Access Denied: Missing video capability token.", {
        status: 403,
        headers: {
          "Content-Type": "text/plain",
          "Access-Control-Allow-Origin": origin,
          "Access-Control-Allow-Credentials": "true",
        },
      });
    }

    // 3. Verify HMAC-SHA256 Token
    const parts = token.split(".");
    if (parts.length !== 2) {
      return new Response("Access Denied: Invalid token format.", {
        status: 403,
        headers: {
          "Content-Type": "text/plain",
          "Access-Control-Allow-Origin": origin,
          "Access-Control-Allow-Credentials": "true",
        },
      });
    }

    const [b64Payload, providedSig] = parts;

    try {
      const secret = env.HMAC_SECRET || "dev-cloudflare-video-hmac-secret-alphazero-default-change-in-prod-min32!";
      const enc = new TextEncoder();
      const key = await crypto.subtle.importKey(
        "raw",
        enc.encode(secret),
        { name: "HMAC", hash: "SHA-256" },
        false,
        ["sign"]
      );

      const expectedSigBuffer = await crypto.subtle.sign("HMAC", key, enc.encode(b64Payload));
      const expectedSig = Array.from(new Uint8Array(expectedSigBuffer))
        .map(b => b.toString(16).padStart(2, "0"))
        .join("");

      if (!constantTimeEquals(providedSig, expectedSig)) {
        return new Response("Access Denied: Invalid token signature.", {
          status: 403,
          headers: {
            "Content-Type": "text/plain",
            "Access-Control-Allow-Origin": origin,
            "Access-Control-Allow-Credentials": "true",
          },
        });
      }

      // Base64Url decode payload
      const unescapedB64 = b64Payload.replace(/-/g, "+").replace(/_/g, "/");
      const pad = unescapedB64.length % 4 === 0 ? "" : "=".repeat(4 - (unescapedB64.length % 4));
      const jsonStr = atob(unescapedB64 + pad);
      const tokenData = JSON.parse(jsonStr);

      const now = Math.floor(Date.now() / 1000);
      if (tokenData.exp && tokenData.exp < now) {
        return new Response("Access Denied: Capability token has expired.", {
          status: 403,
          headers: {
            "Content-Type": "text/plain",
            "Access-Control-Allow-Origin": origin,
            "Access-Control-Allow-Credentials": "true",
          },
        });
      }

      // Scope verification: path prefix
      if (tokenData.path && !url.pathname.startsWith(tokenData.path)) {
        return new Response("Access Denied: Token path does not match requested resource.", {
          status: 403,
          headers: {
            "Content-Type": "text/plain",
            "Access-Control-Allow-Origin": origin,
            "Access-Control-Allow-Credentials": "true",
          },
        });
      }

      // 4. Authorized: Forward to Private S3 Origin
      const newHeaders = new Headers(request.headers);
      if (env.S3_ORIGIN_AUTH_SECRET) {
        newHeaders.set("X-Origin-Auth", env.S3_ORIGIN_AUTH_SECRET);
      }
      
      // Strip Cookie header to maximize Cloudflare CDN cache hit ratio
      newHeaders.delete("Cookie");

      const originRequest = new Request(request.url, {
        method: request.method,
        headers: newHeaders,
        body: request.body,
        redirect: "follow",
      });

      const originResponse = await fetch(originRequest);

      // Clone response and attach CORS headers
      const responseHeaders = new Headers(originResponse.headers);
      responseHeaders.set("Access-Control-Allow-Origin", origin);
      responseHeaders.set("Access-Control-Allow-Credentials", "true");
      responseHeaders.set("Access-Control-Expose-Headers", "Content-Length, Content-Range, Accept-Ranges, Content-Type");

      return new Response(originResponse.body, {
        status: originResponse.status,
        statusText: originResponse.statusText,
        headers: responseHeaders,
      });

    } catch (err) {
      return new Response(`Authentication Error: ${err.message}`, {
        status: 500,
        headers: {
          "Content-Type": "text/plain",
          "Access-Control-Allow-Origin": origin,
          "Access-Control-Allow-Credentials": "true",
        },
      });
    }
  },
};

/**
 * Constant-time comparison between two hex strings to prevent timing attacks.
 */
function constantTimeEquals(a, b) {
  if (typeof a !== "string" || typeof b !== "string") return false;
  if (a.length !== b.length) return false;
  let result = 0;
  for (let i = 0; i < a.length; i++) {
    result |= a.charCodeAt(i) ^ b.charCodeAt(i);
  }
  return result === 0;
}
