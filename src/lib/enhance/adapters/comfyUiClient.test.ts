import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  comfyFetch,
  readComfyJson,
  readComfyResponseBuffer,
} from './comfyUiClient';

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

describe('ComfyUI client bounds', () => {
  it('rejects a declared response larger than the configured limit', async () => {
    const response = new Response('small', {
      headers: { 'content-length': '9' },
    });

    await expect(readComfyResponseBuffer(response, 8))
      .rejects.toThrow('8-byte limit');
  });

  it('rejects a chunked response after the actual bytes exceed the limit', async () => {
    const response = new Response(new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(new Uint8Array(6));
        controller.enqueue(new Uint8Array(6));
        controller.close();
      },
    }));

    await expect(readComfyResponseBuffer(response, 10))
      .rejects.toThrow('10-byte limit');
  });

  it('parses bounded JSON responses', async () => {
    const response = new Response(JSON.stringify({ ok: true }));
    await expect(readComfyJson<{ ok: boolean }>(response, 1024))
      .resolves.toEqual({ ok: true });
  });

  it('rejects scheme-relative request paths', async () => {
    await expect(comfyFetch('//example.invalid/queue'))
      .rejects.toThrow('root-relative');
  });

  it('aborts an unresponsive ComfyUI request at the configured timeout', async () => {
    vi.useFakeTimers();
    vi.stubEnv('PVU_COMFY_REQUEST_TIMEOUT_MS', '1000');
    vi.stubGlobal('fetch', vi.fn((_url: string | URL | Request, init?: RequestInit) => (
      new Promise<Response>((_resolve, reject) => {
        const signal = init?.signal;
        signal?.addEventListener('abort', () => {
          const error = new Error('aborted');
          error.name = 'AbortError';
          reject(error);
        }, { once: true });
      })
    )));

    const request = comfyFetch('/queue');
    await vi.advanceTimersByTimeAsync(1000);

    await expect(request).rejects.toThrow('timed out after 1000ms');
  });
});
