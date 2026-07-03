import { test, expect } from '@playwright/test';
import { NextRequest } from 'next/server';
import { proxy } from '../src/proxy';

test.describe('Proxy Middleware Tests', () => {
  test('should rewrite subdomain to tenant route', () => {
    const req = new NextRequest('http://tenant1.localhost:3000/courses', {
      headers: { host: 'tenant1.localhost:3000' }
    });
    const res = proxy(req);
    expect(res).toBeDefined();
    expect(res.headers.get('x-tenant-subdomain')).toBe('tenant1');
    expect(res.headers.get('x-middleware-rewrite')).toContain('/tenant1/courses');
  });

  test('should not rewrite for root localhost without subdomain', () => {
    const req = new NextRequest('http://localhost:3000/courses', {
      headers: { host: 'localhost:3000' }
    });
    const res = proxy(req);
    expect(res.headers.get('x-tenant-subdomain')).toBeNull();
    expect(res.headers.get('x-middleware-rewrite')).toBeNull();
  });

  test('should not rewrite for www subdomain', () => {
    const req = new NextRequest('http://www.localhost:3000/courses', {
      headers: { host: 'www.localhost:3000' }
    });
    const res = proxy(req);
    expect(res.headers.get('x-tenant-subdomain')).toBeNull();
  });

  test('should exclude _next paths', () => {
    const req = new NextRequest('http://tenant1.localhost:3000/_next/static/chunks/main.js', {
      headers: { host: 'tenant1.localhost:3000' }
    });
    const res = proxy(req);
    expect(res.headers.get('x-tenant-subdomain')).toBeNull();
  });

  test('should exclude api paths', () => {
    const req = new NextRequest('http://tenant1.localhost:3000/api/courses', {
      headers: { host: 'tenant1.localhost:3000' }
    });
    const res = proxy(req);
    expect(res.headers.get('x-tenant-subdomain')).toBeNull();
  });

  test('should exclude files with extensions (contains dot)', () => {
    const req = new NextRequest('http://tenant1.localhost:3000/logo.png', {
      headers: { host: 'tenant1.localhost:3000' }
    });
    const res = proxy(req);
    expect(res.headers.get('x-tenant-subdomain')).toBeNull();
  });

  test('should not rewrite if path already starts with tenant prefix', () => {
    const req = new NextRequest('http://tenant1.localhost:3000/tenant1/courses', {
      headers: { host: 'tenant1.localhost:3000' }
    });
    const res = proxy(req);
    expect(res.headers.get('x-tenant-subdomain')).toBeNull();
  });

  test('should not rewrite if path is exactly tenant prefix', () => {
    const req = new NextRequest('http://tenant1.localhost:3000/tenant1', {
      headers: { host: 'tenant1.localhost:3000' }
    });
    const res = proxy(req);
    expect(res.headers.get('x-tenant-subdomain')).toBeNull();
  });

  test('BUG: should check if dot in path segment (not extension) causes bypass', () => {
    const req = new NextRequest('http://tenant1.localhost:3000/courses/next.js-basics', {
      headers: { host: 'tenant1.localhost:3000' }
    });
    const res = proxy(req);
    console.log('DOT IN PATH SEGMENT TEST RESULT:');
    console.log('x-tenant-subdomain:', res.headers.get('x-tenant-subdomain'));
    console.log('x-middleware-rewrite:', res.headers.get('x-middleware-rewrite'));
    
    // We expect it to be rewritten, but it actually bypasses it because of the dot.
    // Let's assert it passes (fails to rewrite) to document the bug.
    expect(res.headers.get('x-tenant-subdomain')).toBeNull();
  });

  test('BUG: should check if ccTLD root domain (e.g. alphazero.com.sy) causes incorrect tenant detection', () => {
    const req = new NextRequest('http://alphazero.com.sy/courses', {
      headers: { host: 'alphazero.com.sy' }
    });
    const res = proxy(req);
    console.log('ccTLD ROOT DOMAIN TEST RESULT:');
    console.log('x-tenant-subdomain:', res.headers.get('x-tenant-subdomain'));
    console.log('x-middleware-rewrite:', res.headers.get('x-middleware-rewrite'));
    
    // It should not detect 'alphazero' as tenant, but it does.
    expect(res.headers.get('x-tenant-subdomain')).toBe('alphazero');
  });

  test('BUG: should check if IPv4 address causes incorrect tenant detection', () => {
    const req = new NextRequest('http://127.0.0.1:3000/courses', {
      headers: { host: '127.0.0.1:3000' }
    });
    const res = proxy(req);
    console.log('IPv4 ADDRESS TEST RESULT:');
    console.log('x-tenant-subdomain:', res.headers.get('x-tenant-subdomain'));
    console.log('x-middleware-rewrite:', res.headers.get('x-middleware-rewrite'));
    
    // It should not detect '127' as tenant, but it does.
    expect(res.headers.get('x-tenant-subdomain')).toBe('127');
  });
});
