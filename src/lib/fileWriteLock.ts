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

const inProcessLockTails = new Map<string, Promise<unknown>>();

function delay(ms: number) {
  return new Promise<void>((resolve) => setTimeout(resolve, ms));
}

function inProcessLockKey(lockPath: string) {
  const resolved = path.resolve(lockPath);
  return process.platform === 'win32' ? resolved.toLowerCase() : resolved;
}

async function withInProcessLockTurn<T>(lockPath: string, action: () => Promise<T>): Promise<T> {
  const key = inProcessLockKey(lockPath);
  const previous = inProcessLockTails.get(key) ?? Promise.resolve();
  const current = previous.catch(() => undefined).then(action);
  inProcessLockTails.set(key, current);

  try {
    return await current;
  } finally {
    if (inProcessLockTails.get(key) === current) {
      inProcessLockTails.delete(key);
    }
  }
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

async function removeOrphanedAtomicTemps(target: string) {
  const dir = path.dirname(target);
  const fileName = path.basename(target);
  const browserPrefix = `${path.basename(target, path.extname(target))}-`;
  let names: string[];
  try {
    names = await fs.readdir(dir);
  } catch {
    return;
  }

  await Promise.all(names
    .filter((name) => name.endsWith('.tmp')
      && (name.startsWith(`.${fileName}.`) || name.startsWith(browserPrefix)))
    .map((name) => fs.unlink(path.join(dir, name)).catch(() => {})));
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

  // A process-local FIFO prevents same-runtime callers from racing each other
  // and starving behind the timeout. Every turn still acquires the shared
  // create-new file lock, so Browser/WPF and other-process safety is unchanged.
  return withInProcessLockTurn(lockPath, async () => {
    // Time only this caller's shared-file-lock acquisition. Waiting for an
    // earlier caller in this process must not consume its cross-process budget.
    const startedAt = Date.now();
    await fs.mkdir(path.dirname(target), { recursive: true });

    while (true) {
      let handle: Awaited<ReturnType<typeof fs.open>> | undefined;
      try {
        handle = await fs.open(lockPath, 'wx');
        await handle.writeFile(`${JSON.stringify({ pid: process.pid, createdAtUtc: new Date().toISOString() })}\n`, 'utf8');
        // Owning the shared lock proves no compliant writer can still own a
        // target-specific temp. Remove crash residue from either runtime before
        // beginning the next read/merge/replace transaction.
        await removeOrphanedAtomicTemps(target);
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
  });
}
