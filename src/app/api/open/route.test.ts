import os from "os";
import path from "path";
import { NextRequest } from "next/server";
import { describe, expect, it, vi } from "vitest";

import { createOpenHandler, type OpenRouteDependencies } from "./route";

function openRequest(filePath: string) {
  const url = new URL("http://127.0.0.1/api/open");
  url.searchParams.set("path", filePath);
  return new NextRequest(url, { method: "POST" });
}

function dependencies(indexedPath: string): OpenRouteDependencies {
  return {
    platform: process.platform,
    getIndexedPaths: vi.fn(() => [indexedPath]),
    exists: vi.fn(() => true),
    isSupportedImage: vi.fn(() => true),
    openFile: vi.fn(async () => undefined),
  };
}

describe("open route active-index boundary", () => {
  it("rejects a path outside the active index without launching an application", async () => {
    const indexedPath = path.join(os.tmpdir(), "indexed.png");
    const requestedPath = path.join(os.tmpdir(), "unindexed.png");
    const deps = dependencies(indexedPath);

    const response = await createOpenHandler(deps)(openRequest(requestedPath));

    expect(response.status).toBe(403);
    expect(await response.json()).toEqual({
      error: "Image is not in the active index",
    });
    expect(deps.exists).not.toHaveBeenCalled();
    expect(deps.openFile).not.toHaveBeenCalled();
  });

  it("opens the exact indexed image after existence and type checks", async () => {
    const indexedPath = path.join(os.tmpdir(), "indexed.png");
    const deps = dependencies(indexedPath);

    const response = await createOpenHandler(deps)(openRequest(indexedPath));

    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ success: true });
    expect(deps.exists).toHaveBeenCalledWith(path.resolve(indexedPath));
    expect(deps.isSupportedImage).toHaveBeenCalledWith(
      path.resolve(indexedPath),
    );
    expect(deps.openFile).toHaveBeenCalledWith(path.resolve(indexedPath));
  });
});
