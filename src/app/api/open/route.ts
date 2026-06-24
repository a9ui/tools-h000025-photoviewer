import { NextRequest, NextResponse } from 'next/server';
import { execFile } from 'child_process';
import path from 'path';
import fs from 'fs';
import { isSupportedImagePath } from '@/lib/imageFormats';

export const dynamic = 'force-dynamic';

/**
 * POST /api/open?path=ABSOLUTE_PATH
 *
 * Opens a file in the OS default application (e.g. Windows Photo Viewer).
 * Uses `start ""` on Windows.
 */
export async function POST(request: NextRequest) {
  const filePath = request.nextUrl.searchParams.get('path');

  if (!filePath) {
    return NextResponse.json({ error: 'Missing path' }, { status: 400 });
  }

  const resolved = path.resolve(filePath);

  if (!fs.existsSync(resolved)) {
    return NextResponse.json({ error: 'File not found' }, { status: 404 });
  }

  if (!isSupportedImagePath(resolved)) {
    return NextResponse.json({ error: 'Unsupported image type' }, { status: 415 });
  }

  return new Promise<Response>((resolve) => {
    const command = process.platform === 'win32'
      ? 'cmd.exe'
      : process.platform === 'darwin'
        ? 'open'
        : 'xdg-open';
    const args = process.platform === 'win32'
      ? ['/c', 'start', '', resolved]
      : [resolved];

    execFile(command, args, { windowsHide: true }, (err) => {
      if (err) {
        resolve(NextResponse.json({ error: String(err) }, { status: 500 }));
      } else {
        resolve(NextResponse.json({ success: true }));
      }
    });
  });
}
