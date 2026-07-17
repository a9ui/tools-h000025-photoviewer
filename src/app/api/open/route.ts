import { NextRequest, NextResponse } from "next/server";
import { execFile } from "child_process";
import path from "path";
import fs from "fs";
import { isSupportedImagePath } from "@/lib/imageFormats";
import { getIndex } from "@/lib/indexer";
import { findActiveIndexedImagePath } from "@/lib/activeImagePath";

export const dynamic = "force-dynamic";

/**
 * POST /api/open?path=ABSOLUTE_PATH
 *
 * Opens a file in the OS default application (e.g. Windows Photo Viewer).
 * Uses `start ""` on Windows.
 */
export interface OpenRouteDependencies {
  platform: NodeJS.Platform;
  getIndexedPaths: (indexToken?: string) => string[];
  exists: (filePath: string) => boolean;
  isSupportedImage: (filePath: string) => boolean;
  openFile: (filePath: string) => Promise<void>;
}

function openWithDefaultApplication(filePath: string): Promise<void> {
  return new Promise<void>((resolve, reject) => {
    const command =
      process.platform === "win32"
        ? "cmd.exe"
        : process.platform === "darwin"
          ? "open"
          : "xdg-open";
    const args =
      process.platform === "win32" ? ["/c", "start", "", filePath] : [filePath];

    execFile(command, args, { windowsHide: true }, (error) => {
      if (error) reject(error);
      else resolve();
    });
  });
}

const defaultDependencies: OpenRouteDependencies = {
  platform: process.platform,
  getIndexedPaths: (indexToken) =>
    getIndex(indexToken).map((image) => image.absolutePath),
  exists: fs.existsSync,
  isSupportedImage: isSupportedImagePath,
  openFile: openWithDefaultApplication,
};

export function createOpenHandler(
  dependencies: OpenRouteDependencies = defaultDependencies,
) {
  return async function openImage(request: NextRequest) {
    const filePath = request.nextUrl.searchParams.get("path");
    const indexToken =
      request.nextUrl.searchParams.get("indexToken") || undefined;

    if (!filePath) {
      return NextResponse.json({ error: "Missing path" }, { status: 400 });
    }

    const indexedPath = findActiveIndexedImagePath(
      filePath,
      dependencies.getIndexedPaths(indexToken),
      dependencies.platform,
    );
    if (!indexedPath) {
      if (indexToken) {
        return NextResponse.json(
          { error: "Image is not in this viewer session" },
          { status: 404 },
        );
      }
      return NextResponse.json(
        { error: "Image is not in the active index" },
        { status: 403 },
      );
    }

    const resolved = path.resolve(indexedPath);

    if (!dependencies.exists(resolved)) {
      return NextResponse.json({ error: "File not found" }, { status: 404 });
    }

    if (!dependencies.isSupportedImage(resolved)) {
      return NextResponse.json(
        { error: "Unsupported image type" },
        { status: 415 },
      );
    }

    try {
      await dependencies.openFile(resolved);
      return NextResponse.json({ success: true });
    } catch (error) {
      return NextResponse.json({ error: String(error) }, { status: 500 });
    }
  };
}

export const POST = createOpenHandler();
