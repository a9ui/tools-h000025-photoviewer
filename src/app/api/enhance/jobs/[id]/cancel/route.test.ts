import crypto from 'crypto';
import fs from 'fs';
import os from 'os';
import path from 'path';
import { NextRequest } from 'next/server';
import sharp from 'sharp';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { EnhancementJobStore, setEnhancementJobStoreForTests } from '@/lib/enhance/jobStore';
import {
  getEnhancementWorkerInstanceId,
  isEnhancementQueueRunning,
  resetEnhancementQueueForTests,
} from '@/lib/enhance/queue';
import { POST as retryJob } from '@/app/api/enhance/jobs/[id]/retry/route';
import { POST as cancelJob } from './route';

function sha256(filePath: string) {
  return crypto.createHash('sha256').update(fs.readFileSync(filePath)).digest('hex');
}

function removeTempFixture(target: string) {
  const resolvedTemp = path.resolve(os.tmpdir());
  const resolvedTarget = path.resolve(target);
  const relative = path.relative(resolvedTemp, resolvedTarget);
  if (!relative || relative === '..' || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) {
    throw new Error(`Refusing to remove non-TEMP fixture: ${resolvedTarget}`);
  }
  fs.rmSync(resolvedTarget, { recursive: true, force: true });
}

async function waitForQueue(timeoutMs = 15_000) {
  const startedAt = Date.now();
  while (isEnhancementQueueRunning() && Date.now() - startedAt < timeoutMs) {
    await new Promise((resolve) => setTimeout(resolve, 25));
  }
  if (isEnhancementQueueRunning()) throw new Error('Timed out waiting for enhancement queue');
}

describe('explicit enhancement cancellation after restart', () => {
  let fixtureRoot: string;
  let enhanceRoot: string;
  let sourcePath: string;
  let sourceHash: string;
  let store: EnhancementJobStore;
  let originalEnhanceRoot: string | undefined;

  beforeEach(async () => {
    fixtureRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'pvu-enhance-cancel-route-'));
    enhanceRoot = path.join(fixtureRoot, 'enhance');
    sourcePath = path.join(fixtureRoot, 'source.png');
    await sharp({
      create: {
        width: 16,
        height: 16,
        channels: 4,
        background: '#335577ff',
      },
    }).png().toFile(sourcePath);
    sourceHash = sha256(sourcePath);
    originalEnhanceRoot = process.env.PVU_ENHANCE_ROOT;
    process.env.PVU_ENHANCE_ROOT = enhanceRoot;
    resetEnhancementQueueForTests();
    store = new EnhancementJobStore(enhanceRoot);
    setEnhancementJobStoreForTests(store);
  });

  afterEach(async () => {
    await waitForQueue();
    resetEnhancementQueueForTests();
    if (originalEnhanceRoot === undefined) delete process.env.PVU_ENHANCE_ROOT;
    else process.env.PVU_ENHANCE_ROOT = originalEnhanceRoot;
    removeTempFixture(fixtureRoot);
  });

  it('cancels a stale running job immediately and allows the next explicit retry', async () => {
    const stat = fs.statSync(sourcePath);
    const original = await store.createJob({
      sourceId: sourcePath,
      sourcePath,
      sourceSignature: { size: stat.size, mtimeMs: stat.mtimeMs },
      adapterId: 'sharp-test',
    });
    await store.claimNextQueuedJob('enhance-worker-before-restart');

    const cancelResponse = await cancelJob(
      new NextRequest(`http://127.0.0.1/api/enhance/jobs/${original.id}/cancel`, { method: 'POST' }),
      { params: Promise.resolve({ id: original.id }) },
    );
    expect(cancelResponse.status).toBe(200);
    await expect(cancelResponse.json()).resolves.toMatchObject({
      job: {
        id: original.id,
        status: 'canceled',
        cancelRequested: true,
        workerInstanceId: 'enhance-worker-before-restart',
      },
      interruptWarning: '',
    });
    expect(isEnhancementQueueRunning()).toBe(false);

    const retryResponse = await retryJob(
      new NextRequest(`http://127.0.0.1/api/enhance/jobs/${original.id}/retry`, { method: 'POST' }),
      { params: Promise.resolve({ id: original.id }) },
    );
    expect(retryResponse.status).toBe(202);
    const retried = await retryResponse.json();
    expect(retried.job).toMatchObject({ status: 'queued' });
    expect(retried.job.id).not.toBe(original.id);

    await waitForQueue();
    expect((await store.getJob(original.id))?.status).toBe('canceled');
    expect((await store.getJob(retried.job.id))?.status).toBe('succeeded');
    expect(sha256(sourcePath)).toBe(sourceHash);
  }, 15_000);

  it('keeps same-process running cancellation on the existing cancelRequested path', async () => {
    const stat = fs.statSync(sourcePath);
    const original = await store.createJob({
      sourceId: sourcePath,
      sourcePath,
      sourceSignature: { size: stat.size, mtimeMs: stat.mtimeMs },
      adapterId: 'sharp-test',
    });
    await store.claimNextQueuedJob(getEnhancementWorkerInstanceId());

    const response = await cancelJob(
      new NextRequest(`http://127.0.0.1/api/enhance/jobs/${original.id}/cancel`, { method: 'POST' }),
      { params: Promise.resolve({ id: original.id }) },
    );
    expect(response.status).toBe(200);
    await expect(response.json()).resolves.toMatchObject({
      job: {
        id: original.id,
        status: 'running',
        cancelRequested: true,
        workerInstanceId: getEnhancementWorkerInstanceId(),
      },
      interruptWarning: '',
    });
    expect((await store.getJob(original.id))?.finishedAt).toBeUndefined();
    expect(isEnhancementQueueRunning()).toBe(false);
    expect(sha256(sourcePath)).toBe(sourceHash);
  });
});
