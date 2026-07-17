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

  it('cleans only target-specific Browser and WPF atomic temp residue after acquiring the lock', async () => {
    const wpfResidue = path.join(root, '.shared.json.crashed-wpf.tmp');
    const browserResidue = path.join(root, 'shared-crashed-browser.tmp');
    const unrelated = path.join(root, 'other-crashed-browser.tmp');
    await Promise.all([
      fs.writeFile(wpfResidue, 'wpf orphan', 'utf8'),
      fs.writeFile(browserResidue, 'browser orphan', 'utf8'),
      fs.writeFile(unrelated, 'keep', 'utf8'),
    ]);

    await withFileWriteLock(target, async () => {
      await expect(fs.stat(wpfResidue)).rejects.toMatchObject({ code: 'ENOENT' });
      await expect(fs.stat(browserResidue)).rejects.toMatchObject({ code: 'ENOENT' });
      expect(await fs.readFile(unrelated, 'utf8')).toBe('keep');
    });

    expect(await fs.readFile(unrelated, 'utf8')).toBe('keep');
  });
});
