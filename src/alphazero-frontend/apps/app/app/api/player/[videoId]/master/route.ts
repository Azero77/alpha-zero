import { type NextRequest, NextResponse } from "next/server";
import { getSession } from "@/lib/session";

const CDN_BASE_URL = process.env.CDN_BASE_URL || "https://cdn.alphazero.academy";

export async function GET(
  request: NextRequest,
  props: { params: Promise<{ videoId: string }> }
) {
  const { videoId } = await props.params;
  const session = await getSession();

  if (!session) {
    return new NextResponse("Unauthorized", { status: 401 });
  }

  const tenantId = session.tenantId;
  const search = request.nextUrl.search; // preserves ?courseId=...&itemId=...
  const cdnUrl = `${CDN_BASE_URL}/streaming/${tenantId}/${videoId}/master.m3u8`;

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

    // Rewrite variant playlists to point to the BFF media playlist proxy
    const lines = rawManifest.split("\n");
    const rewrittenLines = lines.map((line) => {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith("#")) {
        return line;
      }

      // Variant playlist URI
      if (trimmed.endsWith(".m3u8")) {
        const mediaProxyPath = `/api/player/${videoId}/media/${trimmed}${search}`;
        return mediaProxyPath;
      }

      return line;
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
    return new NextResponse(`Failed to fetch master manifest: ${(error as Error).message}`, {
      status: 502,
    });
  }
}
