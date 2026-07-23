import { type NextRequest, NextResponse } from 'next/server';

import { guardLocalApiRequest, guardLocalImageRequest } from '@/lib/localApiGuard';

export function proxy(request: NextRequest) {
  const guard = request.nextUrl.pathname === '/api/image'
    ? guardLocalImageRequest
    : guardLocalApiRequest;
  return guard(request) ?? NextResponse.next();
}

export const config = {
  matcher: ['/api/:path*'],
};
