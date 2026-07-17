import { promises as fs } from 'fs';
import path from 'path';

export interface FileWriteLockOptions {
  timeoutMs?: number;
  retryDelayMs?: number;
  staleMs?: number;
}

const DEFAULT_TIMEOUT_MS = 2_000;
const DEFAULT_RETRY_DELAY_MS = 25;
const DEFAULT_STALE_MS = 30_000;

function delay(ms: number) {
  return new Promise<void>((resolve) => setTimeout(resolve, ms));
}

async function removeStaleLock(lockPath: string, staleMs: number) {
  try {
    const stat = await fs.stat(lockPath);
    if (Date.now() - stat.mtimeMs <= staleMs) return false;
    await fs.unlink(lockPath);
    return true;
  } catch (error) {
    if ((error as NodeJS.ErrnoException)?.code === 'ENOENT') return true;
    return false;
  }
}

/**
 * Serializes read/merge/replace operations across Browser and WPF processes.
 * The shared protocol is a create-new `<target>.lock` file. A crashed writer's
 * orphan is recoverable only after the deliberately conservative stale limit.
 */
export async function withFileWriteLock<T>(
  target: string,
  action: () => Promise<T>,
  options: FileWriteLockOptions = {},
): Promise<T> {
  const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
  const retryDelayMs = options.retryDelayMs ?? DEFAULT_RETRY_DELAY_MS;
  const staleMs = options.staleMs ?? DEFAULT_STALE_MS;
  const lockPath = `${target}.lock`;
  const startedAt = Date.now();

  await fs.mkdir(path.dirname(target), { recursive: true });

  while (true) {
    let handle: Awaited<ReturnType<typeof fs.open>> | undefined;
    try {
      handle = await fs.open(lockPath, 'wx');
      await handle.writeFile(`${JSON.stringify({ pid: process.pid, createdAtUtc: new Date().toISOString() })}\n`, 'utf8');
    } catch (error) {
      if (handle) {
        await handle.close().catch(() => {});
        await fs.unlink(lockPath).catch(() => {});
      }
      if ((error as NodeJS.ErrnoException)?.code !== 'EEXIST') throw error;

      await removeStaleLock(lockPath, staleMs);
      if (Date.now() - startedAt >= timeoutMs) {
        throw new Error(`Timed out waiting for shared state lock: ${path.basename(lockPath)}`);
      }
      await delay(retryDelayMs);
      continue;
    }

    try {
      return await action();
    } finally {
      await handle!.close().catch(() => {});
      await fs.unlink(lockPath).catch(() => {});
    }
  }
}
