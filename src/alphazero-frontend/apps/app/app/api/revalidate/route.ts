import { revalidateTag } from "next/cache";
import { type NextRequest, NextResponse } from "next/server";

const EXPECTED_SECRET =
  process.env.REVALIDATION_SECRET ||
  process.env.FRONTEND_REVALIDATION_SECRET ||
  "dev-revalidation-secret";

async function handleRevalidate(request: NextRequest) {
  const secretHeader = request.headers.get("x-revalidate-secret");
  const secretParam = request.nextUrl.searchParams.get("secret");
  const providedSecret = secretHeader || secretParam;

  if (providedSecret !== EXPECTED_SECRET) {
    return NextResponse.json(
      { error: "Unauthorized: Invalid revalidation secret" },
      { status: 401 }
    );
  }

  const tag = request.nextUrl.searchParams.get("tag");
  if (!tag) {
    return NextResponse.json(
      { error: "Bad Request: Missing 'tag' parameter" },
      { status: 400 }
    );
  }

  try {
    (revalidateTag as (tag: string, profile?: any) => void)(tag, "default");
    return NextResponse.json({
      revalidated: true,
      tag,
      now: Date.now(),
    });
  } catch (error) {
    return NextResponse.json(
      {
        error: "Failed to revalidate tag",
        message: error instanceof Error ? error.message : String(error),
      },
      { status: 500 }
    );
  }
}

export async function POST(request: NextRequest) {
  return handleRevalidate(request);
}

export async function GET(request: NextRequest) {
  return handleRevalidate(request);
}
