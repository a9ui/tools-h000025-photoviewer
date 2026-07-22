import crypto from 'crypto';
import fs from 'fs';
import os from 'os';
import path from 'path';
import { NextRequest } from 'next/server';
import sharp from 'sharp';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { clearIndexSessionsForTests, getIndex, setIndex } from '@/lib/indexer';
import {
  EnhancementJobStore,
  setEnhancementJobStoreForTests,
} from '@/lib/enhance/jobStore';
import {
  isEnhancementQueueRunning,
  resetEnhancementQueueForTests,
} from '@/lib/enhance/queue';
import { DELETE as deleteOutput } from '@/app/api/enhance/jobs/[id]/output/route';
import { GET as getOutput } from '@/app/api/enhance/output/route';
import { GET as getJobs, POST as createJob } from './route';

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

async function waitForJob(
  store: EnhancementJobStore,
  jobId: string,
  timeoutMs = 15_000,
) {
  const startedAt = Date.now();
  while (Date.now() - startedAt < timeoutMs) {
    const job = await store.getJob(jobId);
    if (job?.status === 'failed') throw new Error(job.errorMessage || 'Enhancement failed');
    if (job?.status === 'succeeded' && !isEnhancementQueueRunning()) return job;
    await new Promise((resolve) => setTimeout(resolve, 25));
  }
  throw new Error('Timed out waiting for TEMP enhancement job');
}

describe('Browser enhancement companion TEMP E2E', () => {
  let fixtureRoot: string;
  let enhanceRoot: string;
  let sourceRoot: string;
  let sourcePath: string;
  let store: EnhancementJobStore;
  let originalEnhanceRoot: string | undefined;

  beforeEach(async () => {
    fixtureRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'pvu-enhance-companion-e2e-'));
    enhanceRoot = path.join(fixtureRoot, 'enhance');
    sourceRoot = path.join(fixtureRoot, 'source');
    sourcePath = path.join(sourceRoot, 'unindexed.png');
    fs.mkdirSync(sourceRoot, { recursive: true });
    await sharp({
      create: {
        width: 8,
        height: 8,
        channels: 4,
        background: '#336699ff',
      },
    }).png().toFile(sourcePath);

    originalEnhanceRoot = process.env.PVU_ENHANCE_ROOT;
    process.env.PVU_ENHANCE_ROOT = enhanceRoot;
    resetEnhancementQueueForTests();
    store = new EnhancementJobStore(enhanceRoot);
    setEnhancementJobStoreForTests(store);
    setIndex([]);
    clearIndexSessionsForTests();
  });

  afterEach(() => {
    resetEnhancementQueueForTests();
    setIndex([]);
    clearIndexSessionsForTests();
    if (originalEnhanceRoot === undefined) delete process.env.PVU_ENHANCE_ROOT;
    else process.env.PVU_ENHANCE_ROOT = originalEnhanceRoot;
    removeTempFixture(fixtureRoot);
  });

  it('creates, polls, serves, and deletes an unindexed source job without touching the source', async () => {
    const sourceHashBefore = sha256(sourcePath);
    const sourceDirectoryBefore = fs.readdirSync(sourceRoot);

    const passiveResponse = await getJobs(new NextRequest(
      `http://127.0.0.1/api/enhance/jobs?sourceId=${encodeURIComponent(sourcePath)}`,
    ));
    expect(passiveResponse.status).toBe(200);
    await expect(passiveResponse.json()).resolves.toEqual({ jobs: [] });
    expect(fs.existsSync(path.join(enhanceRoot, 'jobs.json'))).toBe(false);

    const createResponse = await createJob(new NextRequest(
      'http://127.0.0.1/api/enhance/jobs',
      {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ sourceId: sourcePath, adapterId: 'sharp-test' }),
      },
    ));
    expect(createResponse.status).toBe(202);
    const created = await createResponse.json();
    expect(created.sourceRegistration).toBe('explicit-local-file');
    expect(getIndex()).toEqual([]);

    const succeeded = await waitForJob(store, created.job.id);
    expect(succeeded.outputPath).toBeTruthy();
    expect(path.relative(enhanceRoot, succeeded.outputPath!)).not.toMatch(/^\.\./);

    const reloaded = new EnhancementJobStore(enhanceRoot);
    setEnhancementJobStoreForTests(reloaded);
    const querySource = process.platform === 'win32'
      ? fs.realpathSync.native(sourcePath).toUpperCase()
      : fs.realpathSync(sourcePath);
    const pollResponse = await getJobs(new NextRequest(
      `http://127.0.0.1/api/enhance/jobs?sourceId=${encodeURIComponent(querySource)}`,
    ));
    expect(pollResponse.status).toBe(200);
    const polled = await pollResponse.json();
    expect(polled.jobs.map((job: { id: string }) => job.id)).toEqual([created.job.id]);

    const outputResponse = await getOutput(new NextRequest(
      `http://127.0.0.1/api/enhance/output?jobId=${encodeURIComponent(created.job.id)}`,
    ));
    expect(outputResponse.status).toBe(200);
    expect(outputResponse.headers.get('content-type')).toBe('image/webp');
    expect((await outputResponse.arrayBuffer()).byteLength).toBeGreaterThan(0);

    const deleteResponse = await deleteOutput(
      new NextRequest(
        `http://127.0.0.1/api/enhance/jobs/${created.job.id}/output`,
        { method: 'DELETE' },
      ),
      { params: Promise.resolve({ id: created.job.id }) },
    );
    expect(deleteResponse.status).toBe(200);
    expect((await reloaded.getJob(created.job.id))?.status).toBe('deleted');

    expect(sha256(sourcePath)).toBe(sourceHashBefore);
    expect(fs.readdirSync(sourceRoot)).toEqual(sourceDirectoryBefore);
    expect(fs.existsSync(path.join(enhanceRoot, 'jobs.json'))).toBe(true);
  }, 15_000);
});
