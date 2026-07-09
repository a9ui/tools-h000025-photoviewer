import { promises as fs } from 'fs';
import path from 'path';
import { NextResponse } from 'next/server';

import { formatDirSet } from '@/lib/pathSet';
import { buildSharedRecentFolders, normalizeSharedRecentFolders } from '@/lib/recentFolders';

const RECENT_FOLDERS_PATH = path.join(
  /*turbopackIgnore: true*/ process.cwd(),
  '.cache',
  'recent-folders.json',
);

async function readSharedRecentFolders(): Promise<
  { ok: true; recent: ReturnType<typeof normalizeSharedRecentFolders>; malformed: false } |
  { ok: false; recent: ReturnType<typeof normalizeSharedRecentFolders>; malformed: true; error: string }
> {
  try {
    const raw = await fs.readFile(RECENT_FOLDERS_PATH, 'utf8');
    return { ok: true, recent: normalizeSharedRecentFolders(JSON.parse(raw)), malformed: false };
  } catch (error) {
    if ((error as NodeJS.ErrnoException)?.code === 'ENOENT') {
      return { ok: true, recent: normalizeSharedRecentFolders({}), malformed: false };
    }
    return {
      ok: false,
      recent: normalizeSharedRecentFolders({}),
      malformed: true,
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

async function writeSharedRecentFolders(recent: ReturnType<typeof normalizeSharedRecentFolders>) {
  await fs.mkdir(path.dirname(RECENT_FOLDERS_PATH), { recursive: true });
  await fs.writeFile(RECENT_FOLDERS_PATH, `${JSON.stringify(recent, null, 2)}\n`, 'utf8');
}

export async function GET() {
  const result = await readSharedRecentFolders();
  return NextResponse.json({
    ok: result.ok,
    recent: result.recent,
    malformed: result.malformed,
    error: result.ok ? undefined : result.error,
  }, { status: result.ok ? 200 : 200 });
}

export async function PUT(req: Request) {
  const current = await readSharedRecentFolders();
  if (!current.ok) {
    return NextResponse.json({
      ok: false,
      recent: current.recent,
      malformed: true,
      error: 'Shared recent folders JSON is malformed; refusing to overwrite it.',
    }, { status: 409 });
  }

  const body = await req.json().catch(() => ({}));
  const incoming = buildSharedRecentFolders({
    recentDirs: Array.isArray((body as { recentDirs?: unknown }).recentDirs)
      ? (body as { recentDirs: unknown[] }).recentDirs.filter((item): item is string => typeof item === 'string')
      : [],
    lastDirSet: typeof (body as { lastDirSet?: unknown }).lastDirSet === 'string'
      ? (body as { lastDirSet: string }).lastDirSet
      : '',
  });
  const incomingRecentDirs = incoming.recentFolderSets.map((folderSet) => formatDirSet(folderSet));
  const existingRecentDirs = current.recent.recentFolderSets.map((folderSet) => formatDirSet(folderSet));
  const recent = buildSharedRecentFolders({
    lastDirSet: formatDirSet(incoming.lastFolderSet),
    recentDirs: [
      ...incomingRecentDirs,
      ...existingRecentDirs,
    ],
  });

  await writeSharedRecentFolders(recent);
  return NextResponse.json({ ok: true, recent, malformed: false });
}
