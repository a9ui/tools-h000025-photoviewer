import crypto from "crypto";
import fs from "fs";
import os from "os";
import path from "path";
import { NextRequest } from "next/server";
import sharp from "sharp";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { clearIndexSessionsForTests, getIndex, setIndex } from "@/lib/indexer";
import {
  getEnhancementIsolationMetrics,
  resetEnhancementIsolationMetricsForTests,
} from "@/lib/enhance/isolationMetrics";
import {
  EnhancementJobStore,
  setEnhancementJobStoreForTests,
} from "@/lib/enhance/jobStore";
import { startEnhancementQueue } from "@/lib/enhance/queue";
import { ENHANCEMENT_PRESETS, SHARP_TEST_PRESET } from "@/lib/enhance/types";
import type { ImageFile } from "@/lib/types";
import { GET, POST } from "./route";

vi.mock("@/lib/enhance/queue", () => ({
  startEnhancementQueue: vi.fn(),
}));

vi.mock("@/lib/enhance/adapters/ncnnConfig", () => ({
  getNcnnVulkanAvailability: () => ({ available: true }),
}));

function createRequest(body: Record<string, unknown>, url = "http://127.0.0.1/api/enhance/jobs") {
  return new NextRequest(url, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });
}

function sourceJobsRequest(sourceId: string) {
  return new NextRequest(
    `http://127.0.0.1/api/enhance/jobs?sourceId=${encodeURIComponent(sourceId)}`,
  );
}

function sha256(filePath: string) {
  return crypto.createHash("sha256").update(fs.readFileSync(filePath)).digest("hex");
}

function removeTempFixture(target: string) {
  const resolvedTemp = path.resolve(os.tmpdir());
  const resolvedTarget = path.resolve(target);
  const relative = path.relative(resolvedTemp, resolvedTarget);
  if (!relative || relative === ".." || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) {
    throw new Error(`Refusing to remove non-TEMP fixture: ${resolvedTarget}`);
  }
  fs.rmSync(resolvedTarget, { recursive: true, force: true });
}

describe("POST /api/enhance/jobs preset validation", () => {
  let fixtureRoot: string;
  let enhanceRoot: string;
  let store: EnhancementJobStore;
  let source: ImageFile;
  let originalEnhanceRoot: string | undefined;

  beforeEach(async () => {
    fixtureRoot = fs.mkdtempSync(path.join(os.tmpdir(), "pvu-enhance-route-"));
    enhanceRoot = path.join(fixtureRoot, "enhance");
    const sourceRoot = path.join(fixtureRoot, "sources");
    fs.mkdirSync(sourceRoot, { recursive: true });
    originalEnhanceRoot = process.env.PVU_ENHANCE_ROOT;
    process.env.PVU_ENHANCE_ROOT = enhanceRoot;
    store = new EnhancementJobStore(enhanceRoot);
    setEnhancementJobStoreForTests(store);
    resetEnhancementIsolationMetricsForTests();
    vi.mocked(startEnhancementQueue).mockClear();

    const sourcePath = path.join(sourceRoot, "source.png");
    await sharp({
      create: {
        width: 4,
        height: 4,
        channels: 4,
        background: "#224466ff",
      },
    }).png().toFile(sourcePath);
    const stat = fs.statSync(sourcePath);
    source = {
      id: sourcePath,
      filename: path.basename(sourcePath),
      absolutePath: sourcePath,
      fileUrl: "/api/image?path=source.png&thumb=true",
      displayUrl: "/api/image?path=source.png&display=true",
      fullUrl: "/api/image?path=source.png",
      metadata: null,
      createdAt: stat.birthtimeMs,
      mtime: stat.mtimeMs,
    };
    setIndex([source]);
  });

  afterEach(() => {
    setIndex([]);
    clearIndexSessionsForTests();
    if (originalEnhanceRoot === undefined) delete process.env.PVU_ENHANCE_ROOT;
    else process.env.PVU_ENHANCE_ROOT = originalEnhanceRoot;
    removeTempFixture(fixtureRoot);
  });

  it("accepts a known preset ID", async () => {
    const preset = ENHANCEMENT_PRESETS[1];
    const response = await POST(
      createRequest({
        sourceId: source.id,
        presetId: preset.id,
        adapterId: "sharp-test",
      }),
    );

    expect(response.status).toBe(202);
    const payload = await response.json();
    expect(payload.job).toMatchObject({ presetId: preset.id, preset });
    expect(payload.sourceRegistration).toBe("active-index");
    expect(await store.listJobs()).toHaveLength(1);
    expect(startEnhancementQueue).toHaveBeenCalledOnce();
  });

  it("keeps the existing default when presetId is omitted", async () => {
    const response = await POST(
      createRequest({
        sourceId: source.id,
        adapterId: "sharp-test",
      }),
    );

    expect(response.status).toBe(202);
    const payload = await response.json();
    expect(payload.job).toMatchObject({
      presetId: SHARP_TEST_PRESET.id,
      preset: SHARP_TEST_PRESET,
    });
    expect(await store.listJobs()).toHaveLength(1);
    expect(startEnhancementQueue).toHaveBeenCalledOnce();
  });

  it("rejects an explicit unknown preset without creating or starting work", async () => {
    const response = await POST(
      createRequest({
        sourceId: source.id,
        presetId: "removed-legacy-preset",
        adapterId: "sharp-test",
      }),
    );

    expect(response.status).toBe(400);
    await expect(response.json()).resolves.toEqual({
      error: "Unknown enhancement preset: removed-legacy-preset",
    });
    expect(await store.listJobs()).toEqual([]);
    expect(startEnhancementQueue).not.toHaveBeenCalled();
    expect(getEnhancementIsolationMetrics()).toEqual({
      enhancementEnqueues: 0,
      enhancementWorkerStarts: 0,
    });
  });

  it("creates and reloads a one-shot job for a canonical unindexed TEMP source", async () => {
    setIndex([]);
    const sourceHashBefore = sha256(source.absolutePath);

    const passiveResponse = await GET(sourceJobsRequest(source.absolutePath));
    expect(passiveResponse.status).toBe(200);
    await expect(passiveResponse.json()).resolves.toEqual({ jobs: [] });
    expect(fs.existsSync(path.join(enhanceRoot, "jobs.json"))).toBe(false);

    const response = await POST(createRequest({ sourceId: source.absolutePath }));

    expect(response.status).toBe(202);
    const payload = await response.json();
    const canonicalSource = fs.realpathSync.native(source.absolutePath);
    expect(payload).toMatchObject({
      sourceRegistration: "explicit-local-file",
      job: {
        sourceId: canonicalSource,
        sourcePath: canonicalSource,
        adapterId: "realesrgan-ncnn",
        status: "queued",
      },
    });
    expect(getIndex()).toEqual([]);
    expect(startEnhancementQueue).toHaveBeenCalledOnce();

    const reloaded = new EnhancementJobStore(enhanceRoot);
    await expect(reloaded.getJob(payload.job.id)).resolves.toMatchObject({
      sourceId: canonicalSource,
      sourcePath: canonicalSource,
    });
    setEnhancementJobStoreForTests(reloaded);
    const querySource = process.platform === "win32"
      ? canonicalSource.toUpperCase()
      : canonicalSource;
    const pollResponse = await GET(sourceJobsRequest(querySource));
    expect(pollResponse.status).toBe(200);
    const pollPayload = await pollResponse.json();
    expect(pollPayload.jobs.map((job: { id: string }) => job.id)).toEqual([payload.job.id]);
    expect(sha256(source.absolutePath)).toBe(sourceHashBefore);
  });

  it("rejects non-loopback registration before touching TEMP state", async () => {
    setIndex([]);
    const response = await POST(createRequest(
      { sourceId: source.absolutePath, adapterId: "sharp-test" },
      "http://example.test/api/enhance/jobs",
    ));

    expect(response.status).toBe(403);
    expect(await store.listJobs()).toEqual([]);
    expect(startEnhancementQueue).not.toHaveBeenCalled();
  });

  it("rejects invalid one-shot source boundaries without enqueueing work", async () => {
    setIndex([]);
    const missingPath = path.join(fixtureRoot, "sources", "missing.png");
    const directoryPath = path.join(fixtureRoot, "sources", "folder.png");
    const unsupportedPath = path.join(fixtureRoot, "sources", "source.txt");
    const managedSourcePath = path.join(enhanceRoot, "outputs", "managed.png");
    fs.mkdirSync(directoryPath, { recursive: true });
    fs.writeFileSync(unsupportedPath, "not-an-image");
    fs.mkdirSync(path.dirname(managedSourcePath), { recursive: true });
    fs.copyFileSync(source.absolutePath, managedSourcePath);

    const cases = [
      { sourceId: "relative.png", status: 400, code: "SOURCE_PATH_INVALID" },
      { sourceId: missingPath, status: 404, code: "SOURCE_NOT_FOUND" },
      { sourceId: directoryPath, status: 415, code: "SOURCE_NOT_FILE" },
      { sourceId: unsupportedPath, status: 415, code: "SOURCE_TYPE_UNSUPPORTED" },
      { sourceId: managedSourcePath, status: 403, code: "SOURCE_INSIDE_ENHANCE_ROOT" },
    ];

    for (const testCase of cases) {
      const response = await POST(createRequest({
        sourceId: testCase.sourceId,
        adapterId: "sharp-test",
      }));
      expect(response.status).toBe(testCase.status);
      await expect(response.json()).resolves.toMatchObject({ code: testCase.code });
    }

    expect(await store.listJobs()).toEqual([]);
    expect(startEnhancementQueue).not.toHaveBeenCalled();
  });
});
