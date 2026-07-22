import fs from 'fs';
import path from 'path';
import { canonicalImagePathKey } from '../activeImagePath';
import { isSupportedImagePath } from '../imageFormats';
import { getEnhanceRoot } from './outputPath';

export interface IndexedEnhancementSource {
  id: string;
  absolutePath: string;
}

export interface ResolvedEnhancementSource {
  sourceId: string;
  sourcePath: string;
  sourceSignature: {
    size: number;
    mtimeMs: number;
  };
  registration: 'active-index' | 'explicit-local-file';
}

export type EnhancementSourcePathErrorCode =
  | 'SOURCE_PATH_INVALID'
  | 'SOURCE_NOT_FOUND'
  | 'SOURCE_NOT_FILE'
  | 'SOURCE_TYPE_UNSUPPORTED'
  | 'SOURCE_INSIDE_ENHANCE_ROOT'
  | 'SOURCE_PATH_UNREADABLE';

export class EnhancementSourcePathError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code: EnhancementSourcePathErrorCode,
  ) {
    super(message);
    this.name = 'EnhancementSourcePathError';
  }
}

export type ManagedEnhancementOutputErrorCode =
  | 'OUTPUT_OUTSIDE_MANAGED_ROOT'
  | 'OUTPUT_MISSING'
  | 'OUTPUT_NOT_FILE'
  | 'OUTPUT_PATH_UNREADABLE';

export class ManagedEnhancementOutputError extends Error {
  constructor(
    message: string,
    public readonly code: ManagedEnhancementOutputErrorCode,
  ) {
    super(message);
    this.name = 'ManagedEnhancementOutputError';
  }
}

function pathApi(platform: NodeJS.Platform) {
  return platform === 'win32' ? path.win32 : path.posix;
}

export function enhancementPathKey(
  filePath: string,
  platform: NodeJS.Platform = process.platform,
) {
  return canonicalImagePathKey(filePath, platform);
}

export function enhancementPathsMatch(
  first: string,
  second: string,
  platform: NodeJS.Platform = process.platform,
) {
  return enhancementPathKey(first, platform) === enhancementPathKey(second, platform);
}

export function isPathWithinDirectory(
  rootPath: string,
  candidatePath: string,
  platform: NodeJS.Platform = process.platform,
  allowRoot = false,
) {
  const api = pathApi(platform);
  const relative = api.relative(
    enhancementPathKey(rootPath, platform),
    enhancementPathKey(candidatePath, platform),
  );
  if (relative === '') return allowRoot;
  return relative !== '..'
    && !relative.startsWith(`..${api.sep}`)
    && !api.isAbsolute(relative);
}

export function filterEnhancementJobsBySource<T extends { sourceId: string }>(
  jobs: readonly T[],
  sourceId: string,
  platform: NodeJS.Platform = process.platform,
) {
  const requestedKey = enhancementPathKey(sourceId, platform);
  return jobs.filter((job) => enhancementPathKey(job.sourceId, platform) === requestedKey);
}

async function canonicalEnhanceRoot(enhanceRoot: string) {
  try {
    return await fs.promises.realpath(enhanceRoot);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return null;
    throw error;
  }
}

/**
 * Resolve exactly one source for an explicit enhancement POST. An indexed
 * source keeps the catalog spelling. An unindexed source is a one-shot local
 * registration: it is canonicalized for the job but is never added to the
 * Browser index and its parent folder is never scanned.
 */
export async function resolveEnhancementSource(
  requestedSourceId: string,
  indexedSources: readonly IndexedEnhancementSource[],
  enhanceRoot = getEnhanceRoot(),
  platform: NodeJS.Platform = process.platform,
): Promise<ResolvedEnhancementSource> {
  const indexedSource = indexedSources.find((candidate) =>
    enhancementPathsMatch(candidate.id, requestedSourceId, platform)
    || enhancementPathsMatch(candidate.absolutePath, requestedSourceId, platform));

  if (!indexedSource && !pathApi(platform).isAbsolute(requestedSourceId)) {
    throw new EnhancementSourcePathError(
      'Unindexed enhancement sourceId must be an absolute path',
      400,
      'SOURCE_PATH_INVALID',
    );
  }

  const candidatePath = pathApi(platform).resolve(
    indexedSource?.absolutePath ?? requestedSourceId,
  );
  const resolvedEnhanceRoot = pathApi(platform).resolve(enhanceRoot);
  if (
    !indexedSource
    && isPathWithinDirectory(resolvedEnhanceRoot, candidatePath, platform, true)
  ) {
    throw new EnhancementSourcePathError(
      'Enhancement outputs cannot be registered as source images',
      403,
      'SOURCE_INSIDE_ENHANCE_ROOT',
    );
  }
  if (!isSupportedImagePath(candidatePath)) {
    throw new EnhancementSourcePathError(
      'Unsupported source image type',
      415,
      'SOURCE_TYPE_UNSUPPORTED',
    );
  }

  let canonicalSourcePath: string;
  let stat: fs.Stats;
  try {
    canonicalSourcePath = await fs.promises.realpath(candidatePath);
    stat = await fs.promises.stat(canonicalSourcePath);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      throw new EnhancementSourcePathError(
        'Source image does not exist',
        404,
        'SOURCE_NOT_FOUND',
      );
    }
    throw new EnhancementSourcePathError(
      'Source image path could not be resolved',
      403,
      'SOURCE_PATH_UNREADABLE',
    );
  }
  if (!stat.isFile()) {
    throw new EnhancementSourcePathError(
      'Enhancement source must be a regular file',
      415,
      'SOURCE_NOT_FILE',
    );
  }

  try {
    const canonicalRoot = await canonicalEnhanceRoot(resolvedEnhanceRoot);
    if (
      !indexedSource
      && canonicalRoot
      && isPathWithinDirectory(canonicalRoot, canonicalSourcePath, platform, true)
    ) {
      throw new EnhancementSourcePathError(
        'Enhancement outputs cannot be registered as source images',
        403,
        'SOURCE_INSIDE_ENHANCE_ROOT',
      );
    }
  } catch (error) {
    if (error instanceof EnhancementSourcePathError) throw error;
    throw new EnhancementSourcePathError(
      'Managed enhancement root could not be resolved',
      403,
      'SOURCE_PATH_UNREADABLE',
    );
  }

  return {
    sourceId: indexedSource?.id ?? canonicalSourcePath,
    sourcePath: indexedSource?.absolutePath ?? canonicalSourcePath,
    sourceSignature: { size: stat.size, mtimeMs: stat.mtimeMs },
    registration: indexedSource ? 'active-index' : 'explicit-local-file',
  };
}

export interface ResolvedManagedEnhancementOutput {
  path: string;
  stat: fs.Stats;
}

/** Resolve an existing output through both lexical and canonical ownership checks. */
export async function resolveManagedEnhancementOutput(
  outputPath: string,
  enhanceRoot = getEnhanceRoot(),
  platform: NodeJS.Platform = process.platform,
): Promise<ResolvedManagedEnhancementOutput> {
  const outputsRoot = pathApi(platform).resolve(enhanceRoot, 'outputs');
  const lexicalOutput = pathApi(platform).resolve(outputPath);
  if (!isPathWithinDirectory(outputsRoot, lexicalOutput, platform)) {
    throw new ManagedEnhancementOutputError(
      'Output path is outside the managed enhance cache',
      'OUTPUT_OUTSIDE_MANAGED_ROOT',
    );
  }

  let canonicalRoot: string;
  let canonicalOutput: string;
  let stat: fs.Stats;
  try {
    [canonicalRoot, canonicalOutput] = await Promise.all([
      fs.promises.realpath(outputsRoot),
      fs.promises.realpath(lexicalOutput),
    ]);
    stat = await fs.promises.stat(canonicalOutput);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      throw new ManagedEnhancementOutputError(
        'Output file missing',
        'OUTPUT_MISSING',
      );
    }
    throw new ManagedEnhancementOutputError(
      'Output path could not be resolved',
      'OUTPUT_PATH_UNREADABLE',
    );
  }

  if (!isPathWithinDirectory(canonicalRoot, canonicalOutput, platform)) {
    throw new ManagedEnhancementOutputError(
      'Output path is outside the managed enhance cache',
      'OUTPUT_OUTSIDE_MANAGED_ROOT',
    );
  }
  if (!stat.isFile()) {
    throw new ManagedEnhancementOutputError(
      'Output path is not a regular file',
      'OUTPUT_NOT_FILE',
    );
  }

  return { path: canonicalOutput, stat };
}
