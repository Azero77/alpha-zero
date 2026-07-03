import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function proxy(request: NextRequest) {
  const url = request.nextUrl.clone();
  
  // Get hostname (e.g., 'school.alphazero.com', 'localhost:3000') and strip port
  const host = request.headers.get('host') || '';
  const hostname = host.split(':')[0];

  // Regex asset filter whitelisting common static extensions
  const staticExtensionRegex = /\.(png|jpg|jpeg|gif|svg|ico|css|js|json|woff2?|ttf|map|txt|mp3|mp4|webm)$/i;

  // Exclude static files and api routes
  if (
    url.pathname.startsWith('/_next') ||
    url.pathname.startsWith('/api') ||
    staticExtensionRegex.test(url.pathname)
  ) {
    return NextResponse.next();
  }

  // Check if host is a valid IPv4 address
  const ipv4Regex = /^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$/;
  if (ipv4Regex.test(hostname)) {
    return NextResponse.next();
  }

  // Extract subdomain by checking against base domains
  const baseDomains = ['alpha-zero.com', 'alphazero.co.uk', 'alphazero.com.sy', 'localhost'];
  let subdomain: string | null = null;

  for (const baseDomain of baseDomains) {
    if (hostname === baseDomain) {
      subdomain = null;
      break;
    } else if (hostname.endsWith('.' + baseDomain)) {
      const prefix = hostname.slice(0, -(baseDomain.length + 1));
      if (prefix && prefix !== 'www') {
        subdomain = prefix;
      }
      break;
    }
  }

  if (subdomain) {
    // Prevent infinite rewrite loop if already rewritten to the tenant path
    if (url.pathname.startsWith(`/${subdomain}/`) || url.pathname === `/${subdomain}`) {
      return NextResponse.next();
    }
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
