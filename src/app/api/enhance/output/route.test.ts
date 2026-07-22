import crypto from 'crypto';
import fs from 'fs';
import os from 'os';
import path from 'path';
import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  EnhancementJobStore,
  setEnhancementJobStoreForTests,
} from '@/lib/enhance/jobStore';
import { DELETE as deleteOutput } from '@/app/api/enhance/jobs/[id]/output/route';
import { GET as getOutput } from './route';

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

describe('managed enhancement output routes', () => {
  let fixtureRoot: string;
  let enhanceRoot: string;
  let sourcePath: string;
  let store: EnhancementJobStore;
  let originalEnhanceRoot: string | undefined;

  beforeEach(() => {
    fixtureRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'pvu-enhance-output-route-'));
    enhanceRoot = path.join(fixtureRoot, 'enhance');
    sourcePath = path.join(fixtureRoot, 'sources', 'source.png');
    fs.mkdirSync(path.dirname(sourcePath), { recursive: true });
    fs.writeFileSync(sourcePath, 'source-bytes-are-read-only');
    originalEnhanceRoot = process.env.PVU_ENHANCE_ROOT;
    process.env.PVU_ENHANCE_ROOT = enhanceRoot;
    store = new EnhancementJobStore(enhanceRoot);
    setEnhancementJobStoreForTests(store);
  });

  afterEach(() => {
    if (originalEnhanceRoot === undefined) delete process.env.PVU_ENHANCE_ROOT;
    else process.env.PVU_ENHANCE_ROOT = originalEnhanceRoot;
    removeTempFixture(fixtureRoot);
  });

  async function createSucceededJob(outputPath: string) {
    const sourceStat = fs.statSync(sourcePath);
    const job = await store.createJob({
      sourceId: sourcePath,
      sourcePath,
      sourceSignature: { size: sourceStat.size, mtimeMs: sourceStat.mtimeMs },
    });
    await store.updateJob(job.id, {
      status: 'succeeded',
      progress: 100,
      outputPath,
    });
    return job;
  }

  it('serves and deletes a canonical managed output without changing its source', async () => {
    const outputPath = path.join(enhanceRoot, 'outputs', 'owned', 'enhanced.png');
    fs.mkdirSync(path.dirname(outputPath), { recursive: true });
    fs.writeFileSync(outputPath, 'enhanced-output');
    const sourceHashBefore = sha256(sourcePath);
    const job = await createSucceededJob(outputPath);

    const getResponse = await getOutput(new NextRequest(
      `http://127.0.0.1/api/enhance/output?jobId=${encodeURIComponent(job.id)}`,
    ));
    expect(getResponse.status).toBe(200);
    await expect(getResponse.text()).resolves.toBe('enhanced-output');

    const deleteResponse = await deleteOutput(
      new NextRequest(`http://127.0.0.1/api/enhance/jobs/${job.id}/output`, { method: 'DELETE' }),
      { params: Promise.resolve({ id: job.id }) },
    );
    expect(deleteResponse.status).toBe(200);
    expect(fs.existsSync(outputPath)).toBe(false);
    expect(sha256(sourcePath)).toBe(sourceHashBefore);
  });

  it('neither serves nor deletes an output that escapes through a TEMP junction', async () => {
    const outputsRoot = path.join(enhanceRoot, 'outputs');
    const outsideRoot = path.join(fixtureRoot, 'outside-managed-output-root');
    const junctionPath = path.join(outputsRoot, 'escaped');
    const outsideOutput = path.join(outsideRoot, 'enhanced.png');
    fs.mkdirSync(outputsRoot, { recursive: true });
    fs.mkdirSync(outsideRoot, { recursive: true });
    fs.writeFileSync(outsideOutput, 'must-survive');
    fs.symlinkSync(
      outsideRoot,
      junctionPath,
      process.platform === 'win32' ? 'junction' : 'dir',
    );
    const escapedOutputPath = path.join(junctionPath, 'enhanced.png');
    const sourceHashBefore = sha256(sourcePath);
    const outputHashBefore = sha256(outsideOutput);
    const job = await createSucceededJob(escapedOutputPath);

    const getResponse = await getOutput(new NextRequest(
      `http://127.0.0.1/api/enhance/output?jobId=${encodeURIComponent(job.id)}`,
    ));
    expect(getResponse.status).toBe(403);
    await expect(getResponse.text()).resolves.toContain('outside the managed enhance cache');

    const deleteResponse = await deleteOutput(
      new NextRequest(`http://127.0.0.1/api/enhance/jobs/${job.id}/output`, { method: 'DELETE' }),
      { params: Promise.resolve({ id: job.id }) },
    );
    expect(deleteResponse.status).toBe(400);
    await expect(deleteResponse.json()).resolves.toMatchObject({
      error: expect.stringContaining('outside the managed enhance cache'),
    });

    expect((await store.getJob(job.id))?.status).toBe('succeeded');
    expect(fs.existsSync(outsideOutput)).toBe(true);
    expect(sha256(outsideOutput)).toBe(outputHashBefore);
    expect(sha256(sourcePath)).toBe(sourceHashBefore);
  });
});
