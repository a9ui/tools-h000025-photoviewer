import { NextResponse } from 'next/server';
import { getLegacyRecentDirSets } from '@/lib/legacyPhotoviewer';

export const dynamic = 'force-dynamic';

export async function GET() {
  const recentDirs = getLegacyRecentDirSets(12);
  return NextResponse.json({
    recentDirs,
    lastDirSet: recentDirs[0] ?? '',
  });
}
