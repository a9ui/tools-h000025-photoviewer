import fs, { promises as fsPromises } from 'fs';

export const MAX_SHARED_STATE_BYTES = 1_048_576;

export type SharedJsonBytesErrorCode = 'invalid-utf8' | 'too-large';

export class SharedJsonBytesError extends Error {
  constructor(public readonly code: SharedJsonBytesErrorCode, message: string) {
    super(message);
    this.name = 'SharedJsonBytesError';
  }
}

function hasPrefix(bytes: Uint8Array, prefix: readonly number[]) {
  return prefix.every((value, index) => bytes[index] === value);
}

export function decodeStrictUtf8(bytes: Uint8Array, maxBytes = MAX_SHARED_STATE_BYTES) {
  if (bytes.byteLength > maxBytes) {
    throw new SharedJsonBytesError('too-large', `Shared JSON exceeds the ${maxBytes}-byte limit.`);
  }

  let payload = bytes;
  if (hasPrefix(bytes, [0xef, 0xbb, 0xbf])) {
    payload = bytes.subarray(3);
    if (hasPrefix(payload, [0xef, 0xbb, 0xbf])) {
      throw new SharedJsonBytesError('invalid-utf8', 'Shared JSON contains more than one UTF-8 BOM.');
    }
  } else if (
    hasPrefix(bytes, [0xff, 0xfe])
    || hasPrefix(bytes, [0xfe, 0xff])
    || hasPrefix(bytes, [0x00, 0x00, 0xfe, 0xff])
  ) {
    throw new SharedJsonBytesError('invalid-utf8', 'Shared JSON must use UTF-8, not UTF-16 or UTF-32.');
  }

  try {
    return new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(payload);
  } catch {
    throw new SharedJsonBytesError('invalid-utf8', 'Shared JSON contains invalid UTF-8.');
  }
}

export function readStrictUtf8FileSync(target: string, maxBytes = MAX_SHARED_STATE_BYTES) {
  const descriptor = fs.openSync(target, 'r');
  try {
    const chunks: Buffer[] = [];
    let total = 0;
    while (total <= maxBytes) {
      const chunk = Buffer.allocUnsafe(Math.min(64 * 1024, maxBytes + 1 - total));
      const count = fs.readSync(descriptor, chunk, 0, chunk.length, null);
      if (count === 0) break;
      chunks.push(count === chunk.length ? chunk : chunk.subarray(0, count));
      total += count;
    }
    if (total > maxBytes) {
      throw new SharedJsonBytesError('too-large', `Shared JSON exceeds the ${maxBytes}-byte limit.`);
    }
    return decodeStrictUtf8(Buffer.concat(chunks, total), maxBytes);
  } finally {
    fs.closeSync(descriptor);
  }
}

export async function readStrictUtf8File(target: string, maxBytes = MAX_SHARED_STATE_BYTES) {
  const handle = await fsPromises.open(target, 'r');
  try {
    const chunks: Buffer[] = [];
    let total = 0;
    while (total <= maxBytes) {
      const chunk = Buffer.allocUnsafe(Math.min(64 * 1024, maxBytes + 1 - total));
      const { bytesRead } = await handle.read(chunk, 0, chunk.length, null);
      if (bytesRead === 0) break;
      chunks.push(bytesRead === chunk.length ? chunk : chunk.subarray(0, bytesRead));
      total += bytesRead;
    }
    if (total > maxBytes) {
      throw new SharedJsonBytesError('too-large', `Shared JSON exceeds the ${maxBytes}-byte limit.`);
    }
    return decodeStrictUtf8(Buffer.concat(chunks, total), maxBytes);
  } finally {
    await handle.close();
  }
}

export function encodeBoundedJson(
  document: unknown,
  maxBytes = MAX_SHARED_STATE_BYTES,
  space: number | string | undefined = 2,
) {
  const bytes = Buffer.from(`${JSON.stringify(document, null, space)}\n`, 'utf8');
  if (bytes.byteLength > maxBytes) {
    throw new SharedJsonBytesError('too-large', `Shared JSON output exceeds the ${maxBytes}-byte limit.`);
  }
  return bytes;
}
