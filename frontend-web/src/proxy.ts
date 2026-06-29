import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function proxy(request: NextRequest) {
  const url = request.nextUrl.clone();
  
  // Get hostname (e.g., 'school.alphazero.com', 'localhost:3000')
  const hostname = request.headers.get('host') || '';
  
  // Exclude static files and api routes
  if (
    url.pathname.startsWith('/_next') ||
    url.pathname.startsWith('/api') ||
    url.pathname.includes('.')
  ) {
    return NextResponse.next();
  }

  // Extract subdomain (assuming format: subdomain.domain.com)
  // For localhost testing, we might pass a mock or check if it's just 'localhost'
  const isLocalhost = hostname.includes('localhost');
  const parts = hostname.split('.');
  const subdomain = (parts.length >= 3 || (isLocalhost && parts.length >= 2)) ? parts[0] : null;

  if (subdomain && subdomain !== 'www') {
    // Rewrite the URL to a dynamic route for the specific tenant
    url.pathname = `/${subdomain}${url.pathname}`;
    const response = NextResponse.rewrite(url);
    response.headers.set('x-tenant-subdomain', subdomain);
    return response;
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico).*)'],
};
