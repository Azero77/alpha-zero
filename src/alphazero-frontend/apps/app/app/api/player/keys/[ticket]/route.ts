import { type NextRequest, NextResponse } from "next/server";
import { getSession } from "@/lib/session";
import { verifyKeyTicket } from "@/lib/player/ticket";

const API_BASE_URL = process.env.API_BASE_URL || "http://localhost:5000";

export async function GET(
  _request: NextRequest,
  props: { params: Promise<{ ticket: string }> }
) {
  const { ticket } = await props.params;
  const session = await getSession();

  if (!session) {
    return new NextResponse("Unauthorized", { status: 401 });
  }

  // 1. Verify Ticket Cryptographic Signature and Expiry
  const verification = verifyKeyTicket(ticket);
  if (!verification.isValid || !verification.payload) {
    if (verification.error === "Ticket expired") {
      return new NextResponse("Ticket Expired", { status: 410 });
    }
    return new NextResponse(`Forbidden: ${verification.error || "Invalid ticket"}`, {
      status: 403,
    });
  }

  // 2. Validate User Identity matches the ticket owner
  if (verification.payload.uid !== session.userId) {
    return new NextResponse("Forbidden: Ticket does not belong to current user session", {
      status: 403,
    });
  }

  // 3. Fetch the 16-byte raw AES key from .NET Backend using the user's bearer token
  const backendUrl = `${API_BASE_URL}/api/video/keys/${verification.payload.vid}`;

  try {
    const backendResponse = await fetch(backendUrl, {
      method: "GET",
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        "X-Tenant-Id": session.tenantId,
      },
      cache: "no-store",
    });

    if (!backendResponse.ok) {
      return new NextResponse(
        `Backend key service error: ${backendResponse.statusText}`,
        { status: backendResponse.status }
      );
    }

    const keyData = await backendResponse.arrayBuffer();

    return new Response(keyData, {
      status: 200,
      headers: {
        "Content-Type": "application/octet-stream",
        "Cache-Control": "private, no-store, no-cache, max-age=0, must-revalidate",
      },
    });
  } catch (error) {
    return new NextResponse(
      `Failed to retrieve key from backend: ${(error as Error).message}`,
      { status: 502 }
    );
  }
}
