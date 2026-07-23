import fs from "fs";
import os from "os";
import path from "path";
import { NextRequest } from "next/server";
import { afterEach, describe, expect, it, vi } from "vitest";

import { createImageHandler } from "./route";

const createdPaths: string[] = [];

function imageRequest(
  filePath: string,
  indexToken?: string,
  headers: Record<string, string> = {},
) {
  const url = new URL("http://127.0.0.1/api/image");
  url.searchParams.set("path", filePath);
  if (indexToken) url.searchParams.set("indexToken", indexToken);
  return new NextRequest(url, { headers });
}

afterEach(() => {
  for (const filePath of createdPaths.splice(0)) {
    try {
      fs.rmSync(filePath, { force: true });
    } catch {}
  }
});

describe("image route active-index boundary", () => {
  it("rejects an existing-looking image outside the explicit viewer session before file access", async () => {
    const requestedPath = path.join(os.tmpdir(), "photoviewer-unindexed.png");
    const handler = createImageHandler({
      platform: process.platform,
      getIndexedPaths: () => [path.join(os.tmpdir(), "other.png")],
      hasIndexSession: () => true,
    });

    const response = await handler(imageRequest(requestedPath, "idx_current"));

    expect(response.status).toBe(404);
    expect(await response.text()).toBe("Image is not in this viewer session");
  });

  it("serves a same-origin no-cors image held by the explicit viewer session", async () => {
    const indexedPath = path.join(
      os.tmpdir(),
      `photoviewer-indexed-${process.pid}.png`,
    );
    createdPaths.push(indexedPath);
    fs.writeFileSync(indexedPath, Buffer.from([0x89, 0x50, 0x4e, 0x47]));
    const handler = createImageHandler({
      platform: process.platform,
      getIndexedPaths: () => [indexedPath],
      hasIndexSession: () => true,
    });

    const response = await handler(imageRequest(indexedPath, "idx_current", {
      host: "localhost",
      "sec-fetch-site": "same-origin",
      "sec-fetch-mode": "no-cors",
      "sec-fetch-dest": "image",
    }));

    expect(response.status).toBe(200);
    expect(response.headers.get("content-type")).toBe("image/png");
    expect(response.headers.get("cross-origin-resource-policy")).toBe("same-origin");
    expect(response.headers.get("vary")).toBe(
      "Sec-Fetch-Site, Sec-Fetch-Mode, Sec-Fetch-Dest, Origin",
    );
    expect(response.headers.get("x-content-type-options")).toBe("nosniff");
    expect(Array.from(new Uint8Array(await response.arrayBuffer()))).toEqual([
      0x89, 0x50, 0x4e, 0x47,
    ]);
  });

  it("rejects an implicit active-index fallback when no viewer session token is supplied", async () => {
    const requestedPath = path.join(os.tmpdir(), "photoviewer-active-only.png");
    const getIndexedPaths = vi.fn(() => [requestedPath]);
    const handler = createImageHandler({
      platform: process.platform,
      getIndexedPaths,
      hasIndexSession: () => true,
    });

    const response = await handler(imageRequest(requestedPath));

    expect(response.status).toBe(403);
    expect(response.headers.get("cache-control")).toBe("no-store");
    expect(await response.text()).toContain("Explicit viewer session required");
    expect(getIndexedPaths).not.toHaveBeenCalled();
  });

  it("returns 410 for an explicit viewer session that no longer exists without falling back to the active index", async () => {
    const requestedPath = path.join(os.tmpdir(), "photoviewer-expired-session.png");
    const getIndexedPaths = vi.fn(() => [requestedPath]);
    const handler = createImageHandler({
      platform: process.platform,
      getIndexedPaths,
      hasIndexSession: () => false,
    });

    const response = await handler(imageRequest(requestedPath, "idx_expired"));

    expect(response.status).toBe(410);
    expect(response.headers.get("cache-control")).toBe("no-store");
    expect(await response.text()).toContain("viewer session expired");
    expect(getIndexedPaths).not.toHaveBeenCalled();
  });

  it("rejects a cross-site no-cors image before session lookup", async () => {
    const requestedPath = path.join(os.tmpdir(), "photoviewer-cross-site.png");
    const getIndexedPaths = vi.fn(() => [requestedPath]);
    const hasIndexSession = vi.fn(() => true);
    const handler = createImageHandler({
      platform: process.platform,
      getIndexedPaths,
      hasIndexSession,
    });

    const response = await handler(imageRequest(requestedPath, "idx_current", {
      host: "localhost",
      "sec-fetch-site": "cross-site",
      "sec-fetch-mode": "no-cors",
      "sec-fetch-dest": "image",
    }));

    expect(response.status).toBe(403);
    expect(hasIndexSession).not.toHaveBeenCalled();
    expect(getIndexedPaths).not.toHaveBeenCalled();
  });
});
