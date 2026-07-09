import { formatDirSet, parseDirSet } from './pathSet';

export type SharedRecentFolders = {
  version: 1;
  lastFolderSet: string[];
  recentFolderSets: string[][];
  updatedAtUtc: string;
};

export type RecentFolderMemory = {
  recentDirs: string[];
  lastDirSet: string;
};

const MAX_RECENT_FOLDER_SETS = 8;

function normalizeFolderSet(value: unknown): string[] {
  if (Array.isArray(value)) {
    return parseDirSet(value.filter((item): item is string => typeof item === 'string').join('\n'));
  }
  if (typeof value === 'string') {
    return parseDirSet(value);
  }
  return [];
}

function normalizeRecentFolderSets(value: unknown): string[][] {
  if (!Array.isArray(value)) return [];

  const seen = new Set<string>();
  const sets: string[][] = [];
  for (const rawSet of value) {
    const folderSet = normalizeFolderSet(rawSet);
    if (folderSet.length === 0) continue;
    const key = formatDirSet(folderSet).toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    sets.push(folderSet);
    if (sets.length >= MAX_RECENT_FOLDER_SETS) break;
  }
  return sets;
}

export function normalizeSharedRecentFolders(value: unknown, now = new Date().toISOString()): SharedRecentFolders {
  const source = value && typeof value === 'object' && !Array.isArray(value)
    ? value as Record<string, unknown>
    : {};
  const lastFolderSet = normalizeFolderSet(source.lastFolderSet);
  const recentFolderSets = normalizeRecentFolderSets(source.recentFolderSets);
  const updatedAtUtc = typeof source.updatedAtUtc === 'string' && source.updatedAtUtc
    ? source.updatedAtUtc
    : now;

  return {
    version: 1,
    lastFolderSet,
    recentFolderSets,
    updatedAtUtc,
  };
}

export function buildSharedRecentFolders(memory: RecentFolderMemory, now = new Date().toISOString()): SharedRecentFolders {
  const lastFolderSet = parseDirSet(memory.lastDirSet);
  const recentFolderSets = normalizeRecentFolderSets(memory.recentDirs.map((item) => parseDirSet(item)));
  const mergedRecent = normalizeRecentFolderSets([
    lastFolderSet,
    ...recentFolderSets,
  ]);

  return {
    version: 1,
    lastFolderSet,
    recentFolderSets: mergedRecent,
    updatedAtUtc: now,
  };
}

export function sharedRecentToLocalMemory(shared: SharedRecentFolders): RecentFolderMemory {
  const recentDirs = shared.recentFolderSets
    .map((folderSet) => formatDirSet(folderSet))
    .filter(Boolean)
    .slice(0, MAX_RECENT_FOLDER_SETS);
  const lastDirSet = formatDirSet(shared.lastFolderSet);
  return { recentDirs, lastDirSet };
}
