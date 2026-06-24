import { NextResponse } from 'next/server';
import { getTags } from '@/lib/indexer';

export const dynamic = 'force-dynamic';

/**
 * GET /api/tags
 *
 * Returns autocomplete tags extracted from the in-memory index.
 */
export async function GET() {
  const tags = getTags(2000);
  return NextResponse.json({ tags });
}
