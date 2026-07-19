import fs from 'fs';

const DEFAULT_WINDOWS_RETRY_DELAYS_MS = [25, 50, 100, 200, 400, 800, 1600] as const;

export interface EnhancementOutputPublishDependencies {
  rename: (source: string, destination: string) => Promise<void>;
  copyFile: (source: string, destination: string) => Promise<void>;
  remove: (target: string) => Promise<void>;
  wait: (delayMs: number) => Promise<void>;
}

const defaultDependencies: EnhancementOutputPublishDependencies = {
  rename: (source, destination) => fs.promises.rename(source, destination),
  copyFile: (source, destination) => fs.promises.copyFile(source, destination),
  remove: (target) => fs.promises.rm(target, { force: true }),
  wait: (delayMs) => new Promise((resolve) => setTimeout(resolve, delayMs)),
};

export function isTransientEnhancementPublishError(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false;
  const code = (error as NodeJS.ErrnoException).code;
  return code === 'EBUSY' || code === 'EPERM' || code === 'EACCES';
}

/**
 * Publishes a completed enhancement without exposing the adapter's temporary
 * path. Windows scanners, indexers, and recently closed decoders can briefly
 * retain a file handle after Sharp finishes. Retry that narrow failure class,
 * then use a fully awaited copy. The queue marks the job succeeded only after
 * this function returns, so a copied destination is not served mid-write.
 */
export async function publishEnhancementOutput(
  temporaryPath: string,
  destinationPath: string,
  options: {
    dependencies?: Partial<EnhancementOutputPublishDependencies>;
    retryDelaysMs?: readonly number[];
  } = {},
): Promise<'rename' | 'copy'> {
  const dependencies = { ...defaultDependencies, ...options.dependencies };
  const retryDelaysMs = options.retryDelaysMs ?? DEFAULT_WINDOWS_RETRY_DELAYS_MS;
  let lastTransientError: unknown = null;

  for (let attempt = 0; ; attempt += 1) {
    try {
      await dependencies.rename(temporaryPath, destinationPath);
      return 'rename';
    } catch (error) {
      if (!isTransientEnhancementPublishError(error)) throw error;
      lastTransientError = error;
      if (attempt >= retryDelaysMs.length) break;
      await dependencies.wait(retryDelaysMs[attempt]);
    }
  }

  try {
    await dependencies.copyFile(temporaryPath, destinationPath);
    await dependencies.remove(temporaryPath);
    return 'copy';
  } catch (copyError) {
    const error = new Error(
      `Could not publish the enhanced output after Windows released neither rename nor copy access: ${
        copyError instanceof Error ? copyError.message : String(copyError)
      }`,
      { cause: lastTransientError ?? copyError },
    );
    (error as NodeJS.ErrnoException).code = (copyError as NodeJS.ErrnoException)?.code;
    throw error;
  }
}
