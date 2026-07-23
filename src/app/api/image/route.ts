import { NextRequest } from "next/server";
import { guardLocalImageRequest } from "@/lib/localApiGuard";
import fs from "fs";
import path from "path";
import { Readable } from "stream";
import { ensureDisplayImage, ensureThumbnail, type ImageWorkTiming } from "@/lib/thumbnailCache";
import { getImageContentType, isSupportedImagePath } from "@/lib/imageFormats";
import { getIndex, hasIndexSession } from "@/lib/indexer";
import { findActiveIndexedImagePath } from "@/lib/activeImagePath";
import { formatServerTiming, isPerfTraceEnabled } from "@/lib/perfTrace";

export const dynamic = "force-dynamic";

function getFileResponse(
  filePath: string,
  contentType: string,
  cacheControl: string,
  extraHeaders: Record<string, string> = {},
) {
  const stat = fs.statSync(filePath);
  const stream = Readable.toWeb(
    fs.createReadStream(filePath),
  ) as ReadableStream;
  return new Response(stream, {
    headers: {
      "Content-Type": contentType,
      "Cache-Control": cacheControl,
      "Cross-Origin-Resource-Policy": "same-origin",
      "Content-Length": String(stat.size),
      "Last-Modified": stat.mtime.toUTCString(),
      Vary: "Sec-Fetch-Site, Sec-Fetch-Mode, Sec-Fetch-Dest, Origin",
      "X-Content-Type-Options": "nosniff",
      ETag: `"${stat.size}-${Math.trunc(stat.mtimeMs)}"`,
      ...extraHeaders,
    },
  });
}

function getImageTimingHeaders(
  timing: ImageWorkTiming | undefined,
  routeStartedAt: number,
): Record<string, string> {
  if (!timing) return {};
  return {
    "Server-Timing": formatServerTiming([
      ["cache", timing.cacheMs],
      ["queue", timing.queueMs],
      ["sharp", timing.sharpMs],
      ["work", timing.totalMs],
      ["route", performance.now() - routeStartedAt],
    ]),
    "X-PV-Image-Cache": timing.cacheHit ? "hit" : timing.coalesced ? "coalesced" : "miss",
  };
}

function parseThumbPriority(request: NextRequest, warmOnly: boolean): number {
  const value = (
    request.nextUrl.searchParams.get("priority") ||
    request.headers.get("x-pv-thumb-priority") ||
    ""
  ).toLowerCase();

  if (value === "focused" || value === "current" || value === "modal")
    return -1;
  if (value === "high" || value === "visible") return 0;
  if (value === "low" || value === "preload" || value === "nearby") return 2;
  return warmOnly ? 2 : 1;
}

function parseCacheVersion(request: NextRequest) {
  const value = request.nextUrl.searchParams.get("v");
  if (!value || value.length > 32) return undefined;
  return /^\d+(?:\.\d+)?$/.test(value) ? value : undefined;
}

function getVersionedCacheControl(hasVersion: boolean, versionMatched = true) {
  if (!hasVersion) return "public, max-age=86400";
  return versionMatched
    ? "public, max-age=31536000, immutable"
    : "public, max-age=0, must-revalidate";
}

function getFallbackCacheControl(
  resolved: string,
  cacheVersion: string | undefined,
  hasVersion: boolean,
) {
  if (!cacheVersion)
    return hasVersion
      ? "public, max-age=31536000, immutable"
      : "public, max-age=3600";
  try {
    return String(fs.statSync(resolved).mtimeMs) === cacheVersion
      ? "public, max-age=31536000, immutable"
      : "public, max-age=0, must-revalidate";
  } catch {
    return "public, max-age=0, must-revalidate";
  }
}

/**
 * GET /api/image?path=ABSOLUTE_PATH&thumb=true|false
 *
 * Serves local images.
 * thumb=true: returns a 300px-wide WebP thumbnail (cached on disk)
 * thumb=false: streams the original image
 */
export interface ImageRouteDependencies {
  platform: NodeJS.Platform;
  getIndexedPaths: (indexToken?: string) => string[];
  hasIndexSession: (indexToken: string) => boolean;
}

const defaultDependencies: ImageRouteDependencies = {
  platform: process.platform,
  getIndexedPaths: (indexToken) =>
    getIndex(indexToken).map((image) => image.absolutePath),
  hasIndexSession,
};

export function createImageHandler(
  dependencies: ImageRouteDependencies = defaultDependencies,
) {
  return async function imageResponse(request: NextRequest) {
    const traceEnabled = isPerfTraceEnabled();
    const routeStartedAt = traceEnabled ? performance.now() : 0;
    const forbidden = guardLocalImageRequest(request);
    if (forbidden) return forbidden;

    const filePath = request.nextUrl.searchParams.get("path");
    const thumb = request.nextUrl.searchParams.get("thumb") === "true";
    const display = request.nextUrl.searchParams.get("display") === "true";
    const warmOnly =
      thumb && request.nextUrl.searchParams.get("warm") === "true";
    const priority = parseThumbPriority(request, warmOnly);
    const cacheVersion = parseCacheVersion(request);
    const hasVersion = Boolean(cacheVersion);
    const indexToken =
      request.nextUrl.searchParams.get("indexToken") || undefined;

    if (!indexToken) {
      return new Response("Explicit viewer session required", {
        status: 403,
        headers: { "Cache-Control": "no-store" },
      });
    }

    if (!filePath) {
      return new Response("Missing path", { status: 400 });
    }

    if (!dependencies.hasIndexSession(indexToken)) {
      return new Response("This viewer session expired. Scan the folder set again to refresh it.", {
        status: 410,
        headers: { "Cache-Control": "no-store" },
      });
    }

    const indexedPath = findActiveIndexedImagePath(
      filePath,
      dependencies.getIndexedPaths(indexToken),
      dependencies.platform,
    );
    if (!indexedPath) {
      return new Response("Image is not in this viewer session", {
        status: 404,
      });
    }

    const resolved = path.resolve(indexedPath);

    if (!fs.existsSync(resolved)) {
      return new Response("File not found", { status: 404 });
    }

    if (!isSupportedImagePath(resolved)) {
      return new Response("Unsupported image type", { status: 415 });
    }

    if (display) {
      try {
        const { displayPath, versionMatched = true, timing } = await ensureDisplayImage(
          resolved,
          priority,
          cacheVersion,
          traceEnabled,
        );
        return getFileResponse(
          displayPath,
          "image/webp",
          getVersionedCacheControl(hasVersion, versionMatched),
          getImageTimingHeaders(timing, routeStartedAt),
        );
      } catch {
        return getFileResponse(
          resolved,
          getImageContentType(resolved),
          getFallbackCacheControl(resolved, cacheVersion, hasVersion),
        );
      }
    }

    if (thumb) {
      try {
        const { thumbPath, versionMatched = true, timing } = await ensureThumbnail(
          resolved,
          priority,
          cacheVersion,
          false,
          traceEnabled,
        );

        if (warmOnly) {
          return new Response(null, {
            status: 204,
            headers: {
              "Cache-Control": "no-store",
              ...getImageTimingHeaders(timing, routeStartedAt),
            },
          });
        }

        return getFileResponse(
          thumbPath,
          "image/webp",
          getVersionedCacheControl(hasVersion, versionMatched),
          getImageTimingHeaders(timing, routeStartedAt),
        );
      } catch {
        if (warmOnly) {
          return new Response(null, {
            status: 204,
            headers: { "Cache-Control": "no-store" },
          });
        }
        return getFileResponse(
          resolved,
          getImageContentType(resolved),
          getFallbackCacheControl(resolved, cacheVersion, hasVersion),
        );
      }
    }

    return getFileResponse(
      resolved,
      getImageContentType(resolved),
      getFallbackCacheControl(resolved, cacheVersion, hasVersion),
    );
  };
}

export const GET = createImageHandler();
