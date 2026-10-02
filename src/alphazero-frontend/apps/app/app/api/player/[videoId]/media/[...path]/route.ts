import { type NextRequest, NextResponse } from "next/server";
import { getSession } from "@/lib/session";
import { createKeyTicket } from "@/lib/player/ticket";

const CDN_BASE_URL = process.env.CDN_BASE_URL || "https://cdn.alphazero.academy";

export async function GET(
  request: NextRequest,
  props: { params: Promise<{ videoId: string; path: string[] }> }
) {
  const { videoId, path } = await props.params;
  const session = await getSession();

  if (!session) {
    return new NextResponse("Unauthorized", { status: 401 });
  }

  const tenantId = session.tenantId;
  const joinedPath = path.join("/");
  const variantDir = path.length > 1 ? path.slice(0, -1).join("/") : "";
  const cdnUrl = `${CDN_BASE_URL}/streaming/${tenantId}/${videoId}/${joinedPath}`;

  try {
    const cdnResponse = await fetch(cdnUrl, {
      headers: {
        cookie: request.headers.get("cookie") || "",
      },
      cache: "no-store",
    });

    if (!cdnResponse.ok) {
      return new NextResponse(`CDN responded with ${cdnResponse.status}`, {
        status: cdnResponse.status,
      });
    }

    const rawManifest = await cdnResponse.text();

    // 1. Mint short-lived (120s) signed ticket for key retrieval
    const ticket = createKeyTicket(videoId, session.userId, tenantId);
    const keyUrl = `/api/player/keys/${ticket}`;

    const pathPrefix = variantDir ? `${variantDir}/` : "";
    const baseCdnSegmentUrl = `${CDN_BASE_URL}/streaming/${tenantId}/${videoId}/${pathPrefix}`;

    // 2. Rewrite media playlist lines
    const lines = rawManifest.split("\n");
    const rewrittenLines = lines.map((line) => {
      const trimmed = line.trim();
      if (!trimmed) return line;

      // Rewrite #EXT-X-KEY URI to point to BFF ephemeral key endpoint
      if (trimmed.startsWith("#EXT-X-KEY")) {
        return trimmed.replace(/URI="[^"]+"/, `URI="${keyUrl}"`);
      }

      // Rewrite #EXT-X-MAP initialization segment to absolute CDN URL
      if (trimmed.startsWith("#EXT-X-MAP")) {
        return trimmed.replace(/URI="([^"]+)"/, (_, initFile) => {
          const absoluteInit = initFile.startsWith("http")
            ? initFile
            : `${baseCdnSegmentUrl}${initFile}`;
          return `URI="${absoluteInit}"`;
        });
      }

      // Comments and other tags remain untouched
      if (trimmed.startsWith("#")) {
        return line;
      }

      // Media segment URLs rewritten to absolute CDN URLs (Zero proxying through Next.js)
      if (trimmed.startsWith("http://") || trimmed.startsWith("https://")) {
        return trimmed;
      }

      return `${baseCdnSegmentUrl}${trimmed}`;
    });

    const rewrittenManifest = rewrittenLines.join("\n");

    return new Response(rewrittenManifest, {
      status: 200,
      headers: {
        "Content-Type": "application/vnd.apple.mpegurl",
        "Cache-Control": "private, no-store, no-cache, max-age=0, must-revalidate",
      },
    });
  } catch (error) {
    return new NextResponse(`Failed to fetch media playlist: ${(error as Error).message}`, {
      status: 502,
    });
  }
}
