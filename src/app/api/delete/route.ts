import { NextRequest, NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';
import { execFile } from 'child_process';
import { promisify } from 'util';
import { getIndex, removeFromIndex } from '@/lib/indexer';
import { isSupportedImagePath } from '@/lib/imageFormats';
import { getDisplayPath, getThumbnailPath } from '@/lib/thumbnailCache';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

const execFileAsync = promisify(execFile);

async function moveFileToRecycleBin(filePath: string) {
  if (process.platform !== 'win32') {
    throw new Error('Recycle Bin delete is only supported on Windows in this app.');
  }

  const encodedPath = Buffer.from(filePath, 'utf16le').toString('base64');
  const command = [
    "$ErrorActionPreference = 'Stop'",
    'Add-Type -AssemblyName Microsoft.VisualBasic',
    `$path = [System.Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('${encodedPath}'))`,
    "[Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile($path, 'OnlyErrorDialogs', 'SendToRecycleBin')",
  ].join('; ');
  const encodedCommand = Buffer.from(command, 'utf16le').toString('base64');

  await execFileAsync('powershell.exe', [
    '-NoProfile',
    '-NonInteractive',
    '-ExecutionPolicy',
    'Bypass',
    '-EncodedCommand',
    encodedCommand,
  ], {
    windowsHide: true,
    timeout: 30_000,
  });
}

async function removeDerivedImages(paths: string[]) {
  await Promise.all(paths.map(async (target) => {
    try {
      await fs.promises.rm(target, { force: true });
    } catch {
      // Derived cache cleanup is best-effort; visible image paths can regenerate.
    }
  }));
}

function isInsideDirectory(parent: string, child: string) {
  const relative = path.relative(path.resolve(parent), path.resolve(child));
  return relative === '' || (!relative.startsWith('..') && !path.isAbsolute(relative));
}

/**
 * DELETE /api/delete?path=ABSOLUTE_PATH
 *
 * Sends a local image to the Windows Recycle Bin, removes it from the in-memory
 * index and on-disk index cache, then cleans derived thumbnails/display images.
 */
export async function DELETE(request: NextRequest) {
  const filePath = request.nextUrl.searchParams.get('path');

  if (!filePath) {
    return NextResponse.json({ error: 'Missing path' }, { status: 400 });
  }

  const resolved = path.resolve(filePath);

  // Safety: don't allow deleting files inside the project directory itself
  const projectRoot = process.cwd();
  if (isInsideDirectory(projectRoot, resolved)) {
    return NextResponse.json(
      { error: 'Cannot delete files inside the project directory' },
      { status: 403 }
    );
  }

  const isIndexed = getIndex().some((image) => path.resolve(image.absolutePath) === resolved);
  if (!isIndexed) {
    return NextResponse.json(
      { error: 'Can only delete images from the active index' },
      { status: 403 }
    );
  }

  if (!fs.existsSync(resolved)) {
    return NextResponse.json({ error: 'File not found' }, { status: 404 });
  }

  if (!isSupportedImagePath(resolved)) {
    return NextResponse.json({ error: 'Unsupported image type' }, { status: 415 });
  }

  try {
    const derivedPaths = await Promise.all([
      getThumbnailPath(resolved),
      getDisplayPath(resolved),
    ]);

    await moveFileToRecycleBin(resolved);

    // Remove from in-memory index and cache
    removeFromIndex(resolved);

    void removeDerivedImages(derivedPaths);

    return NextResponse.json({ success: true, deletedTo: 'recycle-bin' });
  } catch (err) {
    return NextResponse.json({ error: String(err) }, { status: 500 });
  }
}
