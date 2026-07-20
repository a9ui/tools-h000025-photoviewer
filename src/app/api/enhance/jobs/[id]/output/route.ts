import { NextResponse } from 'next/server';
import { getEnhancementJobStore } from '@/lib/enhance/jobStore';
import { guardLocalApiRequest } from '@/lib/localApiGuard';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

export async function DELETE(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const forbidden = guardLocalApiRequest(request);
  if (forbidden) return forbidden;

  const { id } = await params;
  const store = getEnhancementJobStore();
  const existing = await store.getJob(id);
  if (!existing) {
    return NextResponse.json({ error: 'Job not found' }, { status: 404 });
  }
  if (existing.status !== 'succeeded') {
    return NextResponse.json({ error: 'Only completed enhanced outputs can be deleted' }, { status: 409 });
  }
  try {
    const job = await store.deleteOutput(id);
    if (!job) {
      return NextResponse.json({ error: 'Job not found' }, { status: 404 });
    }
    return NextResponse.json({ job });
  } catch (error) {
    return NextResponse.json(
      { error: error instanceof Error ? error.message : String(error) },
      { status: 400 }
    );
  }
}
