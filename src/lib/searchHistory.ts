import { promises as fs } from 'fs';
import path from 'path';

import { withFileWriteLock } from './fileWriteLock';

export const SEARCH_HISTORY_VERSION = 1;
export const MAX_SEARCH_HISTORY_ENTRIES = 50;
export const MAX_SEARCH_HISTORY_QUERY_LENGTH = 32_768;

export interface SearchHistoryDocument extends Record<string, unknown> {
  version: typeof SEARCH_HISTORY_VERSION;
  entries: string[];
  updatedAtUtc: string;
}

export type SearchHistoryReadResult =
  | {
    ok: true;
    entries: string[];
    document: Record<string, unknown>;
    malformed: false;
    futureVersion: false;
  }
  | {
    ok: false;
    entries: [];
    document: Record<string, never>;
    malformed: boolean;
    futureVersion: boolean;
    error: string;
  };

export type SearchHistoryMutation =
  | { action: 'commit'; query: string }
  | { action: 'delete'; query: string }
  | { action: 'clear' };

export interface SearchHistoryMutationResult {
  ok: boolean;
  entries: string[];
  malformed: boolean;
  futureVersion: boolean;
  changed: boolean;
  error?: string;
}

export function normalizeSearchHistoryQuery(query: string) {
  return query
    .split(',')
    .map((token) => token.trim())
    .filter(Boolean)
    .join(', ');
}

export function searchHistoryComparisonKey(query: string) {
  return normalizeSearchHistoryQuery(query).normalize('NFKC').toLocaleLowerCase('en-US');
}

function isObject(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function normalizeEntries(entries: readonly string[]) {
  const normalized: string[] = [];
  const keys = new Set<string>();
  for (const rawEntry of entries) {
    if (rawEntry.length > MAX_SEARCH_HISTORY_QUERY_LENGTH) return null;
    const entry = normalizeSearchHistoryQuery(rawEntry);
    if (!entry) continue;
    const key = searchHistoryComparisonKey(entry);
    if (keys.has(key)) continue;
    keys.add(key);
    normalized.push(entry);
    if (normalized.length >= MAX_SEARCH_HISTORY_ENTRIES) break;
  }
  return normalized;
}

export async function readSearchHistory(target: string): Promise<SearchHistoryReadResult> {
  let parsed: unknown;
  try {
    parsed = JSON.parse(await fs.readFile(target, 'utf8')) as unknown;
  } catch (error) {
    if ((error as NodeJS.ErrnoException)?.code === 'ENOENT') {
      return { ok: true, entries: [], document: {}, malformed: false, futureVersion: false };
    }
    return {
      ok: false,
      entries: [],
      document: {},
      malformed: true,
      futureVersion: false,
      error: error instanceof Error ? error.message : String(error),
    };
  }

  if (!isObject(parsed)) {
    return {
      ok: false,
      entries: [],
      document: {},
      malformed: true,
      futureVersion: false,
      error: 'search-history.json root must be an object.',
    };
  }

  if (typeof parsed.version === 'number'
    && Number.isInteger(parsed.version)
    && parsed.version > SEARCH_HISTORY_VERSION) {
    return {
      ok: false,
      entries: [],
      document: {},
      malformed: false,
      futureVersion: true,
      error: `search-history.json version ${parsed.version} is newer than supported version ${SEARCH_HISTORY_VERSION}.`,
    };
  }

  if (parsed.version !== SEARCH_HISTORY_VERSION
    || !Array.isArray(parsed.entries)
    || !parsed.entries.every((entry) => typeof entry === 'string')
    || (Object.hasOwn(parsed, 'updatedAtUtc') && typeof parsed.updatedAtUtc !== 'string')) {
    return {
      ok: false,
      entries: [],
      document: {},
      malformed: true,
      futureVersion: false,
      error: 'search-history.json does not match the supported version 1 schema.',
    };
  }

  const entries = normalizeEntries(parsed.entries);
  if (entries === null) {
    return {
      ok: false,
      entries: [],
      document: {},
      malformed: true,
      futureVersion: false,
      error: 'search-history.json contains an oversized entry.',
    };
  }

  return {
    ok: true,
    entries,
    document: parsed,
    malformed: false,
    futureVersion: false,
  };
}

function applyMutation(current: readonly string[], mutation: SearchHistoryMutation) {
  if (mutation.action === 'clear') return [];

  const query = normalizeSearchHistoryQuery(mutation.query);
  const key = searchHistoryComparisonKey(query);
  if (!query || !key) return [...current];

  const remaining = current.filter((entry) => searchHistoryComparisonKey(entry) !== key);
  return mutation.action === 'commit'
    ? [query, ...remaining].slice(0, MAX_SEARCH_HISTORY_ENTRIES)
    : remaining;
}

async function writeSearchHistory(
  target: string,
  entries: string[],
  currentDocument: Record<string, unknown>,
) {
  const directory = path.dirname(target);
  const temp = path.join(
    directory,
    `search-history-${process.pid}-${Date.now()}-${Math.random().toString(16).slice(2)}.tmp`,
  );
  const document: SearchHistoryDocument = {
    ...currentDocument,
    version: SEARCH_HISTORY_VERSION,
    entries,
    updatedAtUtc: new Date().toISOString(),
  };
  await fs.mkdir(directory, { recursive: true });
  try {
    await fs.writeFile(temp, `${JSON.stringify(document, null, 2)}\n`, 'utf8');
    await fs.rename(temp, target);
  } finally {
    await fs.unlink(temp).catch(() => {});
  }
}

export async function mutateSearchHistory(
  target: string,
  mutation: SearchHistoryMutation,
): Promise<SearchHistoryMutationResult> {
  return withFileWriteLock(target, async () => {
    const current = await readSearchHistory(target);
    if (!current.ok) {
      return {
        ok: false,
        entries: [],
        malformed: current.malformed,
        futureVersion: current.futureVersion,
        changed: false,
        error: 'Shared search history is malformed or from a newer version; refusing to overwrite it.',
      };
    }

    const entries = applyMutation(current.entries, mutation);
    const changed = entries.length !== current.entries.length
      || entries.some((entry, index) => entry !== current.entries[index]);
    if (changed || !(await fs.stat(target).then(() => true).catch(() => false))) {
      await writeSearchHistory(target, entries, current.document);
    }
    return {
      ok: true,
      entries,
      malformed: false,
      futureVersion: false,
      changed,
    };
  });
}
