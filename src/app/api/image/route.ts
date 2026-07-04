import { NextRequest } from 'next/server';
import fs from 'fs';
import path from 'path';
import { Readable } from 'stream';
import { ensureDisplayImage, ensureThumbnail } from '@/lib/thumbnailCache';
import { getImageContentType, isSupportedImagePath } from '@/lib/imageFormats';

export const dynamic = 'force-dynamic';

function getFileResponse(filePath: string, contentType: string, cacheControl: string) {
  const stat = fs.statSync(filePath);
  const stream = Readable.toWeb(fs.createReadStream(filePath)) as ReadableStream;
  return new Response(stream, {
    headers: {
      'Content-Type': contentType,
      'Cache-Control': cacheControl,
      'Content-Length': String(stat.size),
      'Last-Modified': stat.mtime.toUTCString(),
      ETag: `"${stat.size}-${Math.trunc(stat.mtimeMs)}"`,
    },
  });
}

function parseThumbPriority(request: NextRequest, warmOnly: boolean): number {
  const value = (
    request.nextUrl.searchParams.get('priority') ||
    request.headers.get('x-pv-thumb-priority') ||
    ''
  ).toLowerCase();

  if (value === 'focused' || value === 'current' || value === 'modal') return -1;
  if (value === 'high' || value === 'visible') return 0;
  if (value === 'low' || value === 'preload' || value === 'nearby') return 2;
  return warmOnly ? 2 : 1;
}

function parseCacheVersion(request: NextRequest) {
  const value = request.nextUrl.searchParams.get('v');
  if (!value || value.length > 32) return undefined;
  return /^\d+(?:\.\d+)?$/.test(value) ? value : undefined;
}

function getVersionedCacheControl(hasVersion: boolean, versionMatched = true) {
  if (!hasVersion) return 'public, max-age=86400';
  return versionMatched ? 'public, max-age=31536000, immutable' : 'public, max-age=0, must-revalidate';
}

function getFallbackCacheControl(resolved: string, cacheVersion: string | undefined, hasVersion: boolean) {
  if (!cacheVersion) return hasVersion ? 'public, max-age=31536000, immutable' : 'public, max-age=3600';
  try {
    return String(fs.statSync(resolved).mtimeMs) === cacheVersion
      ? 'public, max-age=31536000, immutable'
      : 'public, max-age=0, must-revalidate';
  } catch {
    return 'public, max-age=0, must-revalidate';
  }
}

/**
 * GET /api/image?path=ABSOLUTE_PATH&thumb=true|false
 *
 * Serves local images.
 * thumb=true: returns a 300px-wide WebP thumbnail (cached on disk)
 * thumb=false: streams the original image
 */
export async function GET(request: NextRequest) {
  const filePath = request.nextUrl.searchParams.get('path');
  const thumb = request.nextUrl.searchParams.get('thumb') === 'true';
  const display = request.nextUrl.searchParams.get('display') === 'true';
  const warmOnly = thumb && request.nextUrl.searchParams.get('warm') === 'true';
  const priority = parseThumbPriority(request, warmOnly);
  const cacheVersion = parseCacheVersion(request);
  const hasVersion = Boolean(cacheVersion);

  if (!filePath) {
    return new Response('Missing path', { status: 400 });
  }

  const resolved = path.resolve(filePath);

  if (!fs.existsSync(resolved)) {
    return new Response('File not found', { status: 404 });
  }

  if (!isSupportedImagePath(resolved)) {
    return new Response('Unsupported image type', { status: 415 });
  }

  if (display) {
    try {
      const { displayPath, versionMatched = true } = await ensureDisplayImage(resolved, priority, cacheVersion);
      return getFileResponse(
        displayPath,
        'image/webp',
        getVersionedCacheControl(hasVersion, versionMatched)
      );
    } catch {
      return getFileResponse(
        resolved,
        getImageContentType(resolved),
        getFallbackCacheControl(resolved, cacheVersion, hasVersion)
      );
    }
  }

  if (thumb) {
    try {
      const { thumbPath, versionMatched = true } = await ensureThumbnail(resolved, priority, cacheVersion);

      if (warmOnly) {
        return new Response(null, {
          status: 204,
          headers: { 'Cache-Control': 'no-store' },
        });
      }

      return getFileResponse(
        thumbPath,
        'image/webp',
        getVersionedCacheControl(hasVersion, versionMatched)
      );
    } catch {
      if (warmOnly) {
        return new Response(null, {
          status: 204,
          headers: { 'Cache-Control': 'no-store' },
        });
      }
      return getFileResponse(resolved, getImageContentType(resolved), getFallbackCacheControl(resolved, cacheVersion, hasVersion));
    }
  }

  return getFileResponse(
    resolved,
    getImageContentType(resolved),
    getFallbackCacheControl(resolved, cacheVersion, hasVersion)
  );
}
