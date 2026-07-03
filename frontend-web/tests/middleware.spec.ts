import { test, expect } from '@playwright/test';
import { proxy } from '../src/proxy';
import { NextRequest, NextResponse } from 'next/server';

test.describe('Subdomain Routing Middleware Unit Tests', () => {
  test('should route requests correctly based on subdomains', () => {
    const request = new NextRequest('http://tenant1.localhost:3000/courses', {
      headers: {
        host: 'tenant1.localhost:3000',
      },
    });

    const response = proxy(request);
    
    // Assert response rewritten path and header
    expect(response).toBeDefined();
    expect(response?.headers.get('x-tenant-subdomain')).toBe('tenant1');
  });

  test('should handle static file exclusions', () => {
    const staticPaths = [
      '/_next/static/chunks/main.js',
      '/api/courses',
      '/logo.png',
      '/favicon.ico',
    ];

    for (const path of staticPaths) {
      const request = new NextRequest(`http://tenant1.localhost:3000${path}`, {
        headers: {
          host: 'tenant1.localhost:3000',
        },
      });

      const response = proxy(request);
      
      expect(response).toBeDefined();
      expect(response?.headers.get('x-tenant-subdomain')).toBeNull();
    }
  });

  test('should route valid paths containing a dot (e.g. math.101) to the subdomain', () => {
    // A path like '/course/lesson-1.2-intro' or '/courses/math.101' is a valid route path,
    // and should be rewritten correctly to the subdomain.
    const request = new NextRequest('http://tenant1.localhost:3000/courses/math.101', {
      headers: {
        host: 'tenant1.localhost:3000',
      },
    });

    const response = proxy(request);

    expect(response?.headers.get('x-tenant-subdomain')).toBe('tenant1');
  });

  test('should guard against infinite redirect/rewrite recursion', () => {
    // If the path is already rewritten to /tenant1/courses
    const request1 = new NextRequest('http://tenant1.localhost:3000/tenant1/courses', {
      headers: {
        host: 'tenant1.localhost:3000',
      },
    });

    const response1 = proxy(request1);
    expect(response1?.headers.get('x-tenant-subdomain')).toBeNull();

    // If the path is already rewritten to /tenant1
    const request2 = new NextRequest('http://tenant1.localhost:3000/tenant1', {
      headers: {
        host: 'tenant1.localhost:3000',
      },
    });

    const response2 = proxy(request2);
    expect(response2?.headers.get('x-tenant-subdomain')).toBeNull();
  });

  test('should handle localhost subdomain formats correctly', () => {
    // e.g. tenant1.localhost:3000 (host header includes port)
    const requestPort = new NextRequest('http://tenant1.localhost:3000/', {
      headers: {
        host: 'tenant1.localhost:3000',
      },
    });
    const responsePort = proxy(requestPort);
    expect(responsePort?.headers.get('x-tenant-subdomain')).toBe('tenant1');

    // e.g. tenant1.localhost (no port)
    const requestNoPort = new NextRequest('http://tenant1.localhost/', {
      headers: {
        host: 'tenant1.localhost',
      },
    });
    const responseNoPort = proxy(requestNoPort);
    expect(responseNoPort?.headers.get('x-tenant-subdomain')).toBe('tenant1');
  });

  test('should not treat www subdomain as a tenant', () => {
    const request = new NextRequest('http://www.localhost:3000/courses', {
      headers: {
        host: 'www.localhost:3000',
      },
    });
    const response = proxy(request);
    expect(response?.headers.get('x-tenant-subdomain')).toBeNull();
  });

  test('should not extract a subdomain when accessing a base domain directly', () => {
    // For a base domain with multiple parts (e.g. 'alphazero.co.uk'), accessing it directly
    // should yield a null subdomain.
    const requestCoUk = new NextRequest('http://alphazero.co.uk/courses', {
      headers: {
        host: 'alphazero.co.uk',
      },
    });
    const responseCoUk = proxy(requestCoUk);
    expect(responseCoUk?.headers.get('x-tenant-subdomain')).toBeNull();
  });
});
