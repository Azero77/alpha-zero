/**
 * AlphaZero Cloudflare Edge Worker: Unified CDN Router
 * 
 * Intercepts media requests under:
 * - /videos/{tenantId}/{videoId}/*
 * - /documents/{tenantId}/{scope}/*
 * 
 * Validates HMAC-SHA256 edge capability cookies (cf_video_token / cf_document_token).
 * Fetches the requested objects directly from the bound R2 buckets (VIDEOS_BUCKET / DOCUMENTS_BUCKET).
 */

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    const origin = request.headers.get("Origin") || "*";

    // 1. CORS Preflight Handling
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

    // 2. Parse Cookies
    const cookieHeader = request.headers.get("Cookie") || "";
    const cookies = Object.fromEntries(
      cookieHeader.split(";").map(c => {
        const [k, ...v] = c.trim().split("=");
        return [k, v.join("=")];
      })
    );

    // 3. Route Request
    try {
      if (url.pathname.startsWith("/streaming/") || url.pathname.startsWith("/videos/")) {
        return await handleR2Request(
          request,
          url,
          env.VIDEOS_BUCKET,
          cookies["cf_video_token"],
          env.VIDEO_HMAC_SECRET || env.HMAC_SECRET,
          origin
        );
      } else {
        // Unrecognized path, could forward to an origin or return 404
        return new Response("Not Found", { status: 404 });
      }
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
 * Validates the HMAC token and fetches the object from the bound R2 bucket.
 */
async function handleR2Request(request, url, bucketBinding, tokenStr, secretKey, origin) {
  if (!tokenStr) {
    return createErrorResponse("Access Denied: Missing capability token.", 403, origin);
  }

  // Verify HMAC-SHA256 Token
  const parts = tokenStr.split(".");
  if (parts.length !== 2) {
    return createErrorResponse("Access Denied: Invalid token format.", 403, origin);
  }

  const [b64Payload, providedSig] = parts;

  // Compute expected signature
  const enc = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw",
    enc.encode(secretKey),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );

  const expectedSigBuffer = await crypto.subtle.sign("HMAC", key, enc.encode(b64Payload));
  const expectedSig = Array.from(new Uint8Array(expectedSigBuffer))
    .map(b => b.toString(16).padStart(2, "0"))
    .join("");

  if (!constantTimeEquals(providedSig, expectedSig)) {
    return createErrorResponse("Access Denied: Invalid token signature.", 403, origin);
  }

  // Base64Url decode payload
  const unescapedB64 = b64Payload.replace(/-/g, "+").replace(/_/g, "/");
  const pad = unescapedB64.length % 4 === 0 ? "" : "=".repeat(4 - (unescapedB64.length % 4));
  const jsonStr = atob(unescapedB64 + pad);
  const tokenData = JSON.parse(jsonStr);

  const now = Math.floor(Date.now() / 1000);
  if (tokenData.exp && tokenData.exp < now) {
    return createErrorResponse("Access Denied: Capability token has expired.", 403, origin);
  }

  // Scope verification: path prefix
  // e.g. tokenData.path = "/documents/tenant-1/course:101/"
  // url.pathname = "/documents/tenant-1/course:101/syllabus.pdf"
  if (tokenData.path && !url.pathname.startsWith(tokenData.path)) {
    return createErrorResponse("Access Denied: Token path does not match requested resource.", 403, origin);
  }

  // 4. Authorized: Fetch from R2 Bucket Binding
  // Note: R2 get() requires the key name without the leading slash
  // If url.pathname is "/documents/tenant/id/file", 
  // Should we strip "/documents/" from the bucket key? 
  // It depends on how you store them in R2. Assuming the bucket root maps to the path root:
  const objectKey = url.pathname.substring(1); 
  
  const object = await bucketBinding.get(objectKey);

  if (object === null) {
    return createErrorResponse("Object Not Found", 404, origin);
  }

  const headers = new Headers();
  headers.set("Access-Control-Allow-Origin", origin);
  headers.set("Access-Control-Allow-Credentials", "true");
  headers.set("Access-Control-Expose-Headers", "Content-Length, Content-Range, Accept-Ranges, Content-Type");
  object.writeHttpMetadata(headers);
  headers.set("etag", object.httpEtag);

  return new Response(object.body, {
    status: 200,
    headers,
  });
}

/**
 * Helper to generate standardized error responses with CORS headers.
 */
function createErrorResponse(message, status, origin) {
  return new Response(message, {
    status: status,
    headers: {
      "Content-Type": "text/plain",
      "Access-Control-Allow-Origin": origin,
      "Access-Control-Allow-Credentials": "true",
    },
  });
}

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
