import fs from 'fs';
import { Readable } from 'stream';
import { NextRequest } from 'next/server';
import { getEnhancementJobStore } from '@/lib/enhance/jobStore';
import { getEnhanceRoot } from '@/lib/enhance/outputPath';
import {
  ManagedEnhancementOutputError,
  resolveManagedEnhancementOutput,
} from '@/lib/enhance/pathContract';
import { getImageContentType } from '@/lib/imageFormats';
import { guardLocalApiRequest } from '@/lib/localApiGuard';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

export async function GET(request: NextRequest) {
  const forbidden = guardLocalApiRequest(request);
  if (forbidden) return forbidden;

  const jobId = request.nextUrl.searchParams.get('jobId');
  if (!jobId) {
    return new Response('Missing jobId', { status: 400 });
  }

  const job = await getEnhancementJobStore().getJob(jobId);
  if (!job || job.status !== 'succeeded' || !job.outputPath) {
    return new Response('Output not found', { status: 404 });
  }

  let output: Awaited<ReturnType<typeof resolveManagedEnhancementOutput>>;
  try {
    output = await resolveManagedEnhancementOutput(job.outputPath, getEnhanceRoot());
  } catch (error) {
    if (error instanceof ManagedEnhancementOutputError) {
      const status = error.code === 'OUTPUT_MISSING' || error.code === 'OUTPUT_NOT_FILE'
        ? 404
        : 403;
      return new Response(error.message, { status });
    }
    throw error;
  }

  const stream = Readable.toWeb(fs.createReadStream(output.path)) as ReadableStream;
  return new Response(stream, {
    headers: {
      'Content-Type': getImageContentType(output.path),
      'Cache-Control': 'private, max-age=3600',
      'Content-Length': String(output.stat.size),
      'Last-Modified': output.stat.mtime.toUTCString(),
      ETag: `"${output.stat.size}-${Math.trunc(output.stat.mtimeMs)}"`,
    },
  });
}
