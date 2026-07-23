import fs from 'fs';
import os from 'os';
import path from 'path';

import {
  acquireLocatorReaderLease,
  acquireLocatorWriterLease,
  LocatorLease,
  LocatorLeaseError,
} from './sharedRootLease';
import { encodeBoundedJson, readStrictUtf8FileSync, SharedJsonBytesError } from './sharedJson';

export const SHARED_ROOT_LOCATOR_PROTOCOL = 'aibos.shared-root-locator/v1';
export const SHARED_ROOT_LOCATOR_VERSION = 1;
export const MAX_SHARED_ROOT_LOCATOR_BYTES = 65_536;
export const SHARED_ROOT_LOCATOR_ENV = 'AIBOS_SHARED_ROOT_LOCATOR_PATH';

export type SharedRootLocatorErrorCode =
  | 'data-root-invalid'
  | 'data-root-unavailable'
  | 'legacy-root-unavailable'
  | 'locator-ambiguous'
  | 'locator-malformed'
  | 'locator-path-invalid'
  | 'locator-too-large'
  | 'locator-unreadable'
  | 'schema-unsupported';

export type SharedRootLocatorResult =
  | { status: 'Resolved'; root: string; locatorPath: string }
  | { status: 'LegacyFallback'; root: string; locatorPath: string }
  | { status: 'Unavailable'; root: null; locatorPath: string | null; errorCode: SharedRootLocatorErrorCode };

export interface SharedRootLocatorOptions {
  legacyRoot: string;
  locatorPath?: string;
  environment?: Readonly<Record<string, string | undefined>>;
}

export type SharedRootActivation = SharedRootLocatorResult & { lease?: LocatorLease };

function unavailable(locatorPath: string | null, errorCode: SharedRootLocatorErrorCode): SharedRootLocatorResult {
  return { status: 'Unavailable', root: null, locatorPath, errorCode };
}

function isInvalidPathText(value: string) {
  return !value || value.trim() !== value || value.includes('\0') || !path.isAbsolute(value);
}

function defaultLocatorPath(environment: Readonly<Record<string, string | undefined>>) {
  const localAppData = environment.LOCALAPPDATA;
  if (typeof localAppData !== 'string' || isInvalidPathText(localAppData)) return null;
  return path.join(path.resolve(localAppData), 'Aibos Image', 'shared-root.v1.json');
}

export function resolveSharedRootLocatorPath(
  explicitPath?: string,
  environment: Readonly<Record<string, string | undefined>> = process.env,
) {
  const configured = explicitPath ?? (
    Object.hasOwn(environment, SHARED_ROOT_LOCATOR_ENV)
      ? environment[SHARED_ROOT_LOCATOR_ENV]
      : undefined
  );
  if (configured !== undefined) {
    if (typeof configured !== 'string' || isInvalidPathText(configured)) return null;
    return path.resolve(configured);
  }
  return defaultLocatorPath(environment);
}

function existingDirectory(value: unknown): { ok: true; root: string } | { ok: false; invalid: boolean } {
  if (typeof value !== 'string' || isInvalidPathText(value)) return { ok: false, invalid: true };
  const resolved = path.resolve(value);
  try {
    return fs.statSync(resolved).isDirectory()
      ? { ok: true, root: fs.realpathSync.native(resolved) }
      : { ok: false, invalid: false };
  } catch (error) {
    return {
      ok: false,
      invalid: (error as NodeJS.ErrnoException).code === 'EINVAL',
    };
  }
}

function missingLocatorIsReadable(locatorPath: string) {
  let current = path.dirname(locatorPath);
  while (true) {
    try {
      return fs.statSync(current).isDirectory();
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code !== 'ENOENT') return false;
      const parent = path.dirname(current);
      if (parent === current) return false;
      current = parent;
    }
  }
}

function skipWhitespace(text: string, start: number) {
  let index = start;
  while (/\s/u.test(text[index] ?? '')) index += 1;
  return index;
}

function stringEnd(text: string, start: number) {
  let index = start + 1;
  while (index < text.length) {
    if (text[index] === '\\') {
      index += 2;
      continue;
    }
    if (text[index] === '"') return index + 1;
    index += 1;
  }
  return text.length;
}

function valueEnd(text: string, start: number) {
  let index = start;
  let depth = 0;
  while (index < text.length) {
    const character = text[index];
    if (character === '"') {
      index = stringEnd(text, index);
      continue;
    }
    if (character === '{' || character === '[') depth += 1;
    if (character === '}' || character === ']') {
      if (depth === 0) return index;
      depth -= 1;
    }
    if (depth === 0 && character === ',') return index;
    index += 1;
  }
  return index;
}

function requiredRootFieldCounts(text: string) {
  const counts = new Map<string, number>();
  let index = skipWhitespace(text, 0);
  if (text[index] !== '{') return counts;
  index += 1;

  while (index < text.length) {
    index = skipWhitespace(text, index);
    if (text[index] === '}') break;
    if (text[index] !== '"') return counts;
    const end = stringEnd(text, index);
    let key: unknown;
    try {
      key = JSON.parse(text.slice(index, end));
    } catch {
      return counts;
    }
    index = skipWhitespace(text, end);
    if (text[index] !== ':') return counts;
    index = skipWhitespace(text, index + 1);
    if (key === 'schemaVersion' || key === 'sharedDataRoot') {
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
    index = skipWhitespace(text, valueEnd(text, index));
    if (text[index] === ',') {
      index += 1;
      continue;
    }
    if (text[index] === '}') break;
    return counts;
  }
  return counts;
}

function readLocator(locatorPath: string): SharedRootLocatorResult {
  let text: string;
  try {
    text = readStrictUtf8FileSync(locatorPath, MAX_SHARED_ROOT_LOCATOR_BYTES);
  } catch (error) {
    const code = (error as NodeJS.ErrnoException).code;
    if (code === 'ENOENT' && missingLocatorIsReadable(locatorPath)) {
      return unavailable(locatorPath, 'locator-unreadable');
    }
    if (error instanceof SharedJsonBytesError) {
      return unavailable(
        locatorPath,
        error.code === 'too-large' ? 'locator-too-large' : 'locator-malformed',
      );
    }
    return unavailable(locatorPath, 'locator-unreadable');
  }

  const counts = requiredRootFieldCounts(text);
  if ((counts.get('schemaVersion') ?? 0) > 1 || (counts.get('sharedDataRoot') ?? 0) > 1) {
    return unavailable(locatorPath, 'locator-ambiguous');
  }

  let document: unknown;
  try {
    document = JSON.parse(text);
  } catch {
    return unavailable(locatorPath, 'locator-malformed');
  }
  if (!document || typeof document !== 'object' || Array.isArray(document)) {
    return unavailable(locatorPath, 'locator-malformed');
  }
  const candidate = document as Record<string, unknown>;
  if (candidate.schemaVersion !== SHARED_ROOT_LOCATOR_VERSION) {
    return unavailable(
      locatorPath,
      typeof candidate.schemaVersion === 'number' && candidate.schemaVersion > SHARED_ROOT_LOCATOR_VERSION
        ? 'schema-unsupported'
        : 'locator-malformed',
    );
  }

  const dataRoot = existingDirectory(candidate.sharedDataRoot);
  if (!dataRoot.ok) {
    return unavailable(locatorPath, dataRoot.invalid ? 'data-root-invalid' : 'data-root-unavailable');
  }
  return { status: 'Resolved', root: dataRoot.root, locatorPath };
}

export function readSharedRootLocator(options: SharedRootLocatorOptions): SharedRootLocatorResult {
  const locatorPath = resolveSharedRootLocatorPath(options.locatorPath, options.environment ?? process.env);
  if (!locatorPath) return unavailable(null, 'locator-path-invalid');

  try {
    const stat = fs.statSync(locatorPath);
    if (!stat.isFile()) return unavailable(locatorPath, 'locator-unreadable');
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== 'ENOENT' || !missingLocatorIsReadable(locatorPath)) {
      return unavailable(locatorPath, 'locator-unreadable');
    }
    const legacy = existingDirectory(options.legacyRoot);
    return legacy.ok
      ? { status: 'LegacyFallback', root: legacy.root, locatorPath }
      : unavailable(locatorPath, 'legacy-root-unavailable');
  }

  return readLocator(locatorPath);
}

export function activateSharedRoot(
  options: SharedRootLocatorOptions & { leaseDirectory?: string },
): SharedRootActivation {
  let lease: LocatorLease;
  try {
    lease = acquireLocatorReaderLease(options.leaseDirectory);
  } catch (error) {
    if (error instanceof LocatorLeaseError) {
      return unavailable(
        resolveSharedRootLocatorPath(options.locatorPath, options.environment ?? process.env),
        'locator-unreadable',
      );
    }
    throw error;
  }

  const result = readSharedRootLocator(options);
  if (result.status === 'Unavailable') {
    lease.close();
    return result;
  }
  return { ...result, lease };
}

export interface SharedRootLocatorMutation {
  mode: 'create' | 'replace';
  locatorPath: string;
  sharedDataRoot: string;
  leaseDirectory?: string;
}

export interface SharedRootLocatorMutationResult {
  ok: boolean;
  acquired: boolean;
  errorCode?: string;
  existedBefore: boolean;
  existsAfter: boolean;
  locatorChanged: boolean;
  resolvedRoot: string | null;
}

export function writeSharedRootLocator(mutation: SharedRootLocatorMutation): SharedRootLocatorMutationResult {
  const locatorPath = resolveSharedRootLocatorPath(mutation.locatorPath, {});
  const existedBefore = Boolean(locatorPath && fs.existsSync(locatorPath));
  if (!locatorPath) {
    return { ok: false, acquired: false, errorCode: 'locator-path-invalid', existedBefore, existsAfter: existedBefore, locatorChanged: false, resolvedRoot: null };
  }
  const dataRoot = existingDirectory(mutation.sharedDataRoot);
  if (!dataRoot.ok) {
    return { ok: false, acquired: false, errorCode: dataRoot.invalid ? 'data-root-invalid' : 'data-root-unavailable', existedBefore, existsAfter: existedBefore, locatorChanged: false, resolvedRoot: null };
  }

  let lease: LocatorLease;
  try {
    lease = acquireLocatorWriterLease(mutation.leaseDirectory);
  } catch (error) {
    const code = error instanceof LocatorLeaseError ? error.code : 'locator-lease-unavailable';
    return { ok: true, acquired: false, errorCode: code, existedBefore, existsAfter: fs.existsSync(locatorPath), locatorChanged: false, resolvedRoot: dataRoot.root };
  }

  let temporaryPath: string | undefined;
  try {
    const existsNow = fs.existsSync(locatorPath);
    if ((mutation.mode === 'create' && existsNow) || (mutation.mode === 'replace' && !existsNow)) {
      return { ok: false, acquired: true, errorCode: existsNow ? 'locator-exists' : 'locator-missing', existedBefore, existsAfter: existsNow, locatorChanged: false, resolvedRoot: dataRoot.root };
    }

    const directory = path.dirname(locatorPath);
    fs.mkdirSync(directory, { recursive: true });
    const bytes = encodeBoundedJson(
      { schemaVersion: SHARED_ROOT_LOCATOR_VERSION, sharedDataRoot: dataRoot.root },
      MAX_SHARED_ROOT_LOCATOR_BYTES,
    );
    temporaryPath = path.join(directory, `.shared-root.v1.${process.pid}.${Date.now()}.tmp`);
    fs.writeFileSync(temporaryPath, bytes, { flag: 'wx' });
    if (mutation.mode === 'create') {
      try {
        fs.linkSync(temporaryPath, locatorPath);
      } catch (error) {
        if ((error as NodeJS.ErrnoException).code === 'EEXIST') {
          return { ok: false, acquired: true, errorCode: 'locator-exists', existedBefore, existsAfter: true, locatorChanged: false, resolvedRoot: dataRoot.root };
        }
        throw error;
      }
      fs.unlinkSync(temporaryPath);
    } else {
      fs.renameSync(temporaryPath, locatorPath);
    }
    temporaryPath = undefined;
    return { ok: true, acquired: true, existedBefore, existsAfter: true, locatorChanged: true, resolvedRoot: dataRoot.root };
  } finally {
    if (temporaryPath) {
      try { fs.unlinkSync(temporaryPath); } catch {}
    }
    lease.close();
  }
}
