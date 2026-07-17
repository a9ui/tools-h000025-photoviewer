import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { withFileWriteLock } from './fileWriteLock';

let root = '';
let target = '';

beforeEach(async () => {
  root = await fs.mkdtemp(path.join(os.tmpdir(), 'pvu-file-lock-'));
  target = path.join(root, 'shared.json');
});

afterEach(async () => {
  await fs.rm(root, { recursive: true, force: true });
});

describe('withFileWriteLock', () => {
  it('serializes concurrent writers and removes the lock afterward', async () => {
    const events: string[] = [];
    let releaseFirst!: () => void;
    const firstCanFinish = new Promise<void>((resolve) => { releaseFirst = resolve; });

    const first = withFileWriteLock(target, async () => {
      events.push('first-start');
      await firstCanFinish;
      events.push('first-end');
    }, { retryDelayMs: 1, timeoutMs: 1_000 });
    await new Promise((resolve) => setTimeout(resolve, 5));
    const second = withFileWriteLock(target, async () => {
      events.push('second-start');
      events.push('second-end');
    }, { retryDelayMs: 1, timeoutMs: 1_000 });

    await new Promise((resolve) => setTimeout(resolve, 5));
    expect(events).toEqual(['first-start']);
    releaseFirst();
    await Promise.all([first, second]);

    expect(events).toEqual(['first-start', 'first-end', 'second-start', 'second-end']);
    await expect(fs.stat(`${target}.lock`)).rejects.toMatchObject({ code: 'ENOENT' });
  });

  it('recovers an orphan only after it is stale', async () => {
    const lockPath = `${target}.lock`;
    await fs.writeFile(lockPath, 'orphan', 'utf8');
    const old = new Date(Date.now() - 60_000);
    await fs.utimes(lockPath, old, old);

    const result = await withFileWriteLock(target, async () => 'recovered', {
      retryDelayMs: 1,
      timeoutMs: 100,
      staleMs: 30_000,
    });

    expect(result).toBe('recovered');
    await expect(fs.stat(lockPath)).rejects.toMatchObject({ code: 'ENOENT' });
  });

  it('times out without deleting a live lock', async () => {
    const lockPath = `${target}.lock`;
    await fs.writeFile(lockPath, 'active', 'utf8');

    await expect(withFileWriteLock(target, async () => 'never', {
      retryDelayMs: 1,
      timeoutMs: 10,
      staleMs: 60_000,
    })).rejects.toThrow(/timed out waiting for shared state lock/i);
    expect(await fs.readFile(lockPath, 'utf8')).toBe('active');
  });
});
