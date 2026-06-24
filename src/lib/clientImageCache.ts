'use client';

type CacheKind = 'thumb' | 'display';

interface CacheEntry {
  objectUrl?: string;
  promise?: Promise<string>;
  lastUsed: number;
}

const MAX_ENTRIES: Record<CacheKind, number> = {
  thumb: 2400,
  display: 160,
};

const caches: Record<CacheKind, Map<string, CacheEntry>> = {
  thumb: new Map(),
  display: new Map(),
};

function trimCache(kind: CacheKind) {
  const cache = caches[kind];
  const limit = MAX_ENTRIES[kind];
  if (cache.size <= limit) return;

  const entries = Array.from(cache.entries())
    .filter(([, entry]) => entry.objectUrl && !entry.promise)
    .sort((a, b) => a[1].lastUsed - b[1].lastUsed);

  for (const [key, entry] of entries) {
    if (cache.size <= limit) break;
    if (entry.objectUrl) URL.revokeObjectURL(entry.objectUrl);
    cache.delete(key);
  }
}

export function getCachedImageUrl(cacheKey: string, kind: CacheKind) {
  const entry = caches[kind].get(cacheKey);
  if (!entry?.objectUrl) return null;
  entry.lastUsed = performance.now();
  return entry.objectUrl;
}

export function evictCachedImageUrl(cacheKey: string, kind: CacheKind) {
  const entry = caches[kind].get(cacheKey);
  if (entry?.objectUrl) URL.revokeObjectURL(entry.objectUrl);
  caches[kind].delete(cacheKey);
}

export function loadCachedImageUrl(cacheKey: string, requestUrl: string, kind: CacheKind) {
  const cache = caches[kind];
  const existing = cache.get(cacheKey);
  if (existing?.objectUrl) {
    existing.lastUsed = performance.now();
    return Promise.resolve(existing.objectUrl);
  }
  if (existing?.promise) return existing.promise;

  const entry: CacheEntry = {
    lastUsed: performance.now(),
  };

  entry.promise = fetch(requestUrl, { cache: 'force-cache' })
    .then((response) => {
      if (!response.ok) throw new Error(`Image fetch failed: ${response.status}`);
      return response.blob();
    })
    .then((blob) => {
      const objectUrl = URL.createObjectURL(blob);
      entry.objectUrl = objectUrl;
      entry.promise = undefined;
      entry.lastUsed = performance.now();
      trimCache(kind);
      return objectUrl;
    })
    .catch((error) => {
      cache.delete(cacheKey);
      throw error;
    });

  cache.set(cacheKey, entry);
  return entry.promise;
}
