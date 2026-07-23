import fs from 'fs';
import path from 'path';

import { activateSharedRoot, SharedRootLocatorErrorCode } from './sharedRootLocator';

const PROCESS_SHARED_ROOT = Symbol.for('h25.shared-root-locator.v1');

interface ProcessSharedRootState {
  root: string;
  locatorPath: string;
  lease: { close(): void };
}

type GlobalWithSharedRoot = typeof globalThis & {
  [PROCESS_SHARED_ROOT]?: ProcessSharedRootState;
};

export class SharedRootUnavailableError extends Error {
  constructor(public readonly code: SharedRootLocatorErrorCode) {
    super(`Aibos shared data root is unavailable (${code}).`);
    this.name = 'SharedRootUnavailableError';
  }
}

function findProjectRoot(start: string) {
  let current = path.resolve(start);
  while (true) {
    if (fs.existsSync(path.join(current, 'project.toml'))) return current;
    const parent = path.dirname(current);
    if (parent === current) return null;
    current = parent;
  }
}

/**
 * Return the checkout that owns user-visible shared state. Linked worktrees
 * point at the main checkout's common Git directory; storing state beside the
 * worktree would otherwise create an invisible second Favorite history.
 */
export function resolveSharedProjectRoot(start = process.cwd()) {
  const projectRoot = findProjectRoot(start) ?? path.resolve(start);
  const dotGitPath = path.join(projectRoot, '.git');
  try {
    if (fs.statSync(dotGitPath).isDirectory()) return projectRoot;
  } catch {
    return projectRoot;
  }

  try {
    const pointer = fs.readFileSync(dotGitPath, 'utf8').split(/\r?\n/, 1)[0]?.trim() ?? '';
    const match = /^gitdir:\s*(.+)$/i.exec(pointer);
    if (!match) return projectRoot;

    const gitDir = path.resolve(projectRoot, match[1]);
    const commonDirValue = fs.readFileSync(path.join(gitDir, 'commondir'), 'utf8').split(/\r?\n/, 1)[0]?.trim();
    if (!commonDirValue) return projectRoot;

    const commonGitDir = path.resolve(gitDir, commonDirValue);
    const mainCheckout = path.dirname(commonGitDir);
    return fs.existsSync(path.join(mainCheckout, 'project.toml')) ? mainCheckout : projectRoot;
  } catch {
    return projectRoot;
  }
}

export type SharedCacheEntry = 'favorites.json' | 'seen.json' | 'recent-folders.json' | 'search-history.json' | 'albums.json' | 'settings.json' | 'enhance';

export interface SharedCacheResolutionOptions {
  locatorPath: string;
  leaseDirectory: string;
  environment?: Readonly<Record<string, string | undefined>>;
}

function resolveSharedDataRoot(start: string, options?: SharedCacheResolutionOptions) {
  const legacyRoot = path.join(resolveSharedProjectRoot(start), '.cache');
  if (options) {
    const activation = activateSharedRoot({
      legacyRoot,
      locatorPath: options.locatorPath,
      leaseDirectory: options.leaseDirectory,
      environment: options.environment,
    });
    if (activation.status === 'Unavailable') throw new SharedRootUnavailableError(activation.errorCode);
    activation.lease?.close();
    return activation.root;
  }

  const sharedGlobal = globalThis as GlobalWithSharedRoot;
  const current = sharedGlobal[PROCESS_SHARED_ROOT];
  if (current) return current.root;

  const activation = activateSharedRoot({ legacyRoot });
  if (activation.status === 'Unavailable' || !activation.lease) {
    throw new SharedRootUnavailableError(
      activation.status === 'Unavailable' ? activation.errorCode : 'locator-unreadable',
    );
  }
  sharedGlobal[PROCESS_SHARED_ROOT] = {
    root: activation.root,
    locatorPath: activation.locatorPath,
    lease: activation.lease,
  };
  return activation.root;
}

export function resolveSharedCachePath(
  fileName: SharedCacheEntry,
  overridePath?: string,
  start = process.cwd(),
  options?: SharedCacheResolutionOptions,
) {
  if (overridePath) return path.resolve(overridePath);

  const sharedDataRoot = resolveSharedDataRoot(start, options);
  // Keep every supported suffix literal. Turbopack otherwise treats a dynamic
  // cache child as a broad filesystem pattern and scans the full thumbnail
  // cache in a real user checkout during production builds.
  switch (fileName) {
    case 'favorites.json':
      return path.join(sharedDataRoot, 'favorites.json');
    case 'seen.json':
      return path.join(sharedDataRoot, 'seen.json');
    case 'recent-folders.json':
      return path.join(sharedDataRoot, 'recent-folders.json');
    case 'search-history.json':
      return path.join(sharedDataRoot, 'search-history.json');
    case 'albums.json':
      return path.join(sharedDataRoot, 'albums.json');
    case 'settings.json':
      return path.join(sharedDataRoot, 'settings.json');
    case 'enhance':
      return path.join(sharedDataRoot, 'enhance');
  }
}
