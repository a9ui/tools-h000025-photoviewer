import fs from 'fs';
import path from 'path';
import { Readable } from 'stream';
import { NextRequest } from 'next/server';
import { getEnhancementJobStore } from '@/lib/enhance/jobStore';
import { getEnhanceRoot } from '@/lib/enhance/outputPath';
import { getImageContentType } from '@/lib/imageFormats';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

export async function GET(request: NextRequest) {
  const jobId = request.nextUrl.searchParams.get('jobId');
  if (!jobId) {
    return new Response('Missing jobId', { status: 400 });
  }

  const job = await getEnhancementJobStore().getJob(jobId);
  if (!job || job.status !== 'succeeded' || !job.outputPath) {
    return new Response('Output not found', { status: 404 });
  }

  const resolved = path.resolve(job.outputPath);
  const outputsRoot = path.resolve(getEnhanceRoot(), 'outputs');
  const relative = path.relative(outputsRoot, resolved);
  if (relative.startsWith('..') || path.isAbsolute(relative)) {
    return new Response('Output path is outside the managed enhance cache', { status: 403 });
  }
  if (!fs.existsSync(resolved)) {
    return new Response('Output file missing', { status: 404 });
  }

  const stat = fs.statSync(resolved);
  const stream = Readable.toWeb(fs.createReadStream(resolved)) as ReadableStream;
  return new Response(stream, {
    headers: {
      'Content-Type': getImageContentType(resolved),
      'Cache-Control': 'private, max-age=3600',
      'Content-Length': String(stat.size),
      'Last-Modified': stat.mtime.toUTCString(),
      ETag: `"${stat.size}-${Math.trunc(stat.mtimeMs)}"`,
    },
  });
}
