import { promises as fs } from 'fs';
import path from 'path';
import { NextResponse } from 'next/server';

import { withFileWriteLock } from '@/lib/fileWriteLock';
import { guardLocalApiRequest } from '@/lib/localApiGuard';
import { formatDirSet } from '@/lib/pathSet';
import { buildSharedRecentFolders, normalizeSharedRecentFolders } from '@/lib/recentFolders';
import { encodeBoundedJson, readStrictUtf8File, SharedJsonBytesError } from '@/lib/sharedJson';
import { resolveSharedCachePath } from '@/lib/sharedProjectRoot';

const MAX_INCOMING_FOLDER_SETS = 100;
const MAX_FOLDER_SET_LENGTH = 32_768;

function recentFoldersPath() {
  return resolveSharedCachePath('recent-folders.json', process.env.PVU_RECENT_FOLDERS_PATH);
}

function isStringOrStringArray(value: unknown) {
  return typeof value === 'string'
    || (Array.isArray(value) && value.every((item) => typeof item === 'string'));
}

function isSupportedStoredDocument(value: unknown) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const source = value as Record<string, unknown>;
  if (Object.hasOwn(source, 'version') && source.version !== 1) return false;
  if (Object.hasOwn(source, 'lastFolderSet') && !isStringOrStringArray(source.lastFolderSet)) return false;
  if (Object.hasOwn(source, 'recentFolderSets')) {
    if (!Array.isArray(source.recentFolderSets)
      || !source.recentFolderSets.every(isStringOrStringArray)) return false;
  }
  if (Object.hasOwn(source, 'updatedAtUtc') && typeof source.updatedAtUtc !== 'string') return false;
  return true;
}

async function readSharedRecentFolders(): Promise<
  { ok: true; recent: ReturnType<typeof normalizeSharedRecentFolders>; document: Record<string, unknown>; malformed: false; futureVersion: false; protected: false; exists: boolean } |
  { ok: false; recent: ReturnType<typeof normalizeSharedRecentFolders>; document: Record<string, never>; malformed: boolean; futureVersion: boolean; protected: true; exists: true; error: string }
> {
  try {
    const raw = await readStrictUtf8File(recentFoldersPath());
    const parsed: unknown = JSON.parse(raw);
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      const version = (parsed as Record<string, unknown>).version;
      if (typeof version === 'number' && Number.isInteger(version) && version > 1) {
        return {
          ok: false,
          recent: normalizeSharedRecentFolders({}),
          document: {},
          malformed: false,
          futureVersion: true,
          protected: true,
          exists: true,
          error: 'recent-folders.json uses a future schema version.',
        };
      }
    }
    if (!isSupportedStoredDocument(parsed)) {
      return {
        ok: false,
        recent: normalizeSharedRecentFolders({}),
        document: {},
        malformed: true,
        futureVersion: false,
        protected: true,
        exists: true,
        error: 'recent-folders.json does not match the supported schema.',
      };
    }
    return {
      ok: true,
      recent: normalizeSharedRecentFolders(parsed),
      document: parsed as Record<string, unknown>,
      malformed: false,
      futureVersion: false,
      protected: false,
      exists: true,
    };
  } catch (error) {
    if ((error as NodeJS.ErrnoException)?.code === 'ENOENT') {
      return { ok: true, recent: normalizeSharedRecentFolders({}), document: {}, malformed: false, futureVersion: false, protected: false, exists: false };
    }
    return {
      ok: false,
      recent: normalizeSharedRecentFolders({}),
      document: {},
      malformed: true,
      futureVersion: false,
      protected: true,
      exists: true,
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

async function writeSharedRecentFolders(
  recent: ReturnType<typeof normalizeSharedRecentFolders>,
  currentDocument: Record<string, unknown>,
) {
  const target = recentFoldersPath();
  const dir = path.dirname(target);
  const temp = path.join(dir, `recent-folders-${process.pid}-${Date.now()}.tmp`);
  const bytes = encodeBoundedJson({ ...currentDocument, ...recent });
  await fs.mkdir(dir, { recursive: true });
  try {
    await fs.writeFile(temp, bytes);
    await fs.rename(temp, target);
  } finally {
    await fs.unlink(temp).catch(() => {});
  }
}

export async function GET() {
  const result = await readSharedRecentFolders();
  return NextResponse.json({
    ok: result.ok,
    recent: result.recent,
    malformed: result.malformed,
    futureVersion: result.futureVersion,
    protected: result.protected,
    exists: result.exists,
    error: result.ok ? undefined : result.error,
  }, { status: result.ok ? 200 : 200 });
}

export async function PUT(req: Request) {
  const forbidden = guardLocalApiRequest(req);
  if (forbidden) return forbidden;

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ ok: false, error: 'Request body must be valid JSON.' }, { status: 400 });
  }
  if (!body || typeof body !== 'object' || Array.isArray(body)) {
    return NextResponse.json({ ok: false, error: 'Request body must be an object.' }, { status: 400 });
  }
  const input = body as Record<string, unknown>;
  if (!Object.hasOwn(input, 'recentDirs') && !Object.hasOwn(input, 'lastDirSet')) {
    return NextResponse.json({ ok: false, error: 'Request body must include recentDirs or lastDirSet.' }, { status: 400 });
  }
  if (Object.hasOwn(input, 'recentDirs') && (
    !Array.isArray(input.recentDirs)
    || input.recentDirs.length > MAX_INCOMING_FOLDER_SETS
    || !input.recentDirs.every((item) => typeof item === 'string' && item.length <= MAX_FOLDER_SET_LENGTH)
  )) {
    return NextResponse.json({ ok: false, error: 'recentDirs must be a bounded string array.' }, { status: 400 });
  }
  if (Object.hasOwn(input, 'lastDirSet') && (
    typeof input.lastDirSet !== 'string'
    || input.lastDirSet.length > MAX_FOLDER_SET_LENGTH
  )) {
    return NextResponse.json({ ok: false, error: 'lastDirSet must be a bounded string.' }, { status: 400 });
  }
  try {
    return await withFileWriteLock(recentFoldersPath(), async () => {
      const current = await readSharedRecentFolders();
      if (!current.ok) {
        return NextResponse.json({
          ok: false,
          recent: current.recent,
          malformed: current.malformed,
          futureVersion: current.futureVersion,
          protected: true,
          error: 'Shared recent folders JSON is malformed; refusing to overwrite it.',
        }, { status: 409 });
      }

      const incoming = buildSharedRecentFolders({
        recentDirs: Array.isArray(input.recentDirs)
          ? input.recentDirs as string[]
          : [],
        lastDirSet: typeof input.lastDirSet === 'string'
          ? input.lastDirSet
          : formatDirSet(current.recent.lastFolderSet),
      });
      const incomingRecentDirs = incoming.recentFolderSets.map((folderSet) => formatDirSet(folderSet));
      const existingRecentDirs = current.recent.recentFolderSets.map((folderSet) => formatDirSet(folderSet));
      const recent = buildSharedRecentFolders({
        lastDirSet: Object.hasOwn(input, 'lastDirSet')
          ? formatDirSet(incoming.lastFolderSet)
          : formatDirSet(current.recent.lastFolderSet),
        recentDirs: [
          ...incomingRecentDirs,
          ...existingRecentDirs,
        ],
      });

      try {
        await writeSharedRecentFolders(recent, current.document);
      } catch (error) {
        if (error instanceof SharedJsonBytesError && error.code === 'too-large') {
          return NextResponse.json({
            ok: false,
            recent: current.recent,
            malformed: false,
            futureVersion: false,
            protected: true,
            error: error.message,
          }, { status: 409 });
        }
        throw error;
      }
      return NextResponse.json({ ok: true, recent, malformed: false });
    });
  } catch (error) {
    return NextResponse.json({
      ok: false,
      error: error instanceof Error ? error.message : String(error),
      malformed: false,
    }, { status: 503 });
  }
}
