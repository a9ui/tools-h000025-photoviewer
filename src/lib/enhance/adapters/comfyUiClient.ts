import { getComfyBaseUrl } from './comfyUiConfig';

const DEFAULT_COMFY_REQUEST_TIMEOUT_MS = 30_000;
const MAX_COMFY_REQUEST_TIMEOUT_MS = 5 * 60_000;
export const DEFAULT_COMFY_RESPONSE_BYTE_LIMIT = 256 * 1024 * 1024;
export const DEFAULT_COMFY_JSON_BYTE_LIMIT = 8 * 1024 * 1024;
const MAX_COMFY_RESPONSE_BYTE_LIMIT = 1024 * 1024 * 1024;

function boundedPositiveInteger(value: string | undefined, fallback: number, maximum: number) {
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed <= 0) return fallback;
  return Math.min(parsed, maximum);
}

export function getComfyRequestTimeoutMs() {
  return boundedPositiveInteger(
    process.env.PVU_COMFY_REQUEST_TIMEOUT_MS,
    DEFAULT_COMFY_REQUEST_TIMEOUT_MS,
    MAX_COMFY_REQUEST_TIMEOUT_MS,
  );
}

export function getComfyResponseByteLimit() {
  return boundedPositiveInteger(
    process.env.PVU_COMFY_MAX_RESPONSE_BYTES,
    DEFAULT_COMFY_RESPONSE_BYTE_LIMIT,
    MAX_COMFY_RESPONSE_BYTE_LIMIT,
  );
}

function assertRelativeComfyPath(pathname: string) {
  if (!pathname.startsWith('/') || pathname.startsWith('//')) {
    throw new Error('ComfyUI request paths must be root-relative.');
  }
}

export async function readComfyResponseBuffer(
  response: Response,
  maximumBytes = getComfyResponseByteLimit(),
) {
  if (!Number.isSafeInteger(maximumBytes) || maximumBytes <= 0) {
    throw new Error('ComfyUI response limit must be a positive safe integer.');
  }

  const contentLength = response.headers.get('content-length');
  if (contentLength && /^\d+$/.test(contentLength)) {
    const declaredLength = Number(contentLength);
    if (!Number.isSafeInteger(declaredLength) || declaredLength > maximumBytes) {
      throw new Error(`ComfyUI response exceeded the ${maximumBytes}-byte limit.`);
    }
  }

  if (!response.body) return Buffer.alloc(0);

  const reader = response.body.getReader();
  const chunks: Buffer[] = [];
  let received = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      if (!value) continue;
      received += value.byteLength;
      if (received > maximumBytes) {
        await reader.cancel('ComfyUI response size limit exceeded.').catch(() => {});
        throw new Error(`ComfyUI response exceeded the ${maximumBytes}-byte limit.`);
      }
      chunks.push(Buffer.from(value));
    }
  } finally {
    reader.releaseLock();
  }
  return Buffer.concat(chunks, received);
}

export async function readComfyJson<T>(
  response: Response,
  maximumBytes = DEFAULT_COMFY_JSON_BYTE_LIMIT,
): Promise<T> {
  const bytes = await readComfyResponseBuffer(response, maximumBytes);
  return JSON.parse(bytes.toString('utf8')) as T;
}

export async function comfyFetch(pathname: string, init: RequestInit = {}) {
  assertRelativeComfyPath(pathname);
  const timeoutMs = getComfyRequestTimeoutMs();
  const controller = new AbortController();
  const upstreamSignal = init.signal;
  let timedOut = false;

  const abortFromUpstream = () => controller.abort();
  if (upstreamSignal?.aborted) controller.abort();
  else upstreamSignal?.addEventListener('abort', abortFromUpstream, { once: true });

  const timeout = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, timeoutMs);

  try {
    const response = await fetch(`${getComfyBaseUrl()}${pathname}`, {
      ...init,
      signal: controller.signal,
    });
    if (!response.ok) {
      const errorBytes = await readComfyResponseBuffer(response, 4096).catch(() => Buffer.alloc(0));
      const text = errorBytes.toString('utf8');
      throw new Error(`ComfyUI ${pathname} failed: HTTP ${response.status}${text ? ` ${text.slice(0, 500)}` : ''}`);
    }
    return response;
  } catch (error) {
    if (timedOut) {
      throw new Error(`ComfyUI ${pathname} timed out after ${timeoutMs}ms.`, { cause: error });
    }
    throw error;
  } finally {
    clearTimeout(timeout);
    upstreamSignal?.removeEventListener('abort', abortFromUpstream);
  }
}

async function getQueue() {
  const response = await comfyFetch('/queue');
  return await readComfyJson<unknown>(response);
}

function queueJsonContainsPromptId(queue: unknown, promptId: string) {
  return JSON.stringify(queue).includes(promptId);
}

export async function requestComfyUiInterrupt(promptId?: string) {
  if (promptId) {
    const queue = await getQueue();
    if (!queueJsonContainsPromptId(queue, promptId)) {
      throw new Error(`ComfyUI prompt ${promptId} is not the active or queued job; refusing global interrupt.`);
    }
  }
  await comfyFetch('/interrupt', { method: 'POST' });
}
