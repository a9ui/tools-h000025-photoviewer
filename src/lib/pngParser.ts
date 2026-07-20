import fs from 'fs';

export interface ParsedMetadata {
  raw: string;
  prompt: string;
  negativePrompt: string;
  settings: Record<string, string>;
}

const PNG_SIGNATURE = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
const PNG_CHUNK_OVERHEAD_BYTES = 12; // length + type + CRC
const MAX_PNG_TEXT_KEYWORD_BYTES = 79;

// Keep Browser metadata limits aligned with the WPF reader. The limit applies
// to the complete tEXt payload, including the keyword and separator byte.
export const MAX_PNG_METADATA_CHUNK_BYTES = 4 * 1024 * 1024;
export const MAX_PNG_TEXT_BYTES_BEFORE_IDAT = 16 * 1024 * 1024;
export const MAX_PNG_CHUNKS_BEFORE_IDAT = 1024;

function readExact(fd: number, buffer: Buffer, position: number): boolean {
  let total = 0;
  while (total < buffer.length) {
    const bytesRead = fs.readSync(
      fd,
      buffer,
      total,
      buffer.length - total,
      position + total,
    );
    if (bytesRead <= 0) return false;
    total += bytesRead;
  }
  return true;
}

/**
 * Read only bounded PNG header chunks (up to IDAT) and extract the first tEXt
 * chunk whose keyword is "parameters".
 *
 * Declared PNG chunk sizes are untrusted. Every chunk must fit inside the
 * actual file before its payload is touched, unrelated tEXt chunks are inspected
 * through a bounded keyword prefix, and the parameters payload has a hard cap.
 */
export function extractSDMetadata(filePath: string): ParsedMetadata | null {
  let fd: number | null = null;
  try {
    fd = fs.openSync(filePath, 'r');
    const stat = fs.fstatSync(fd);
    if (!stat.isFile() || !Number.isSafeInteger(stat.size) || stat.size < PNG_SIGNATURE.length) {
      return null;
    }

    const signature = Buffer.alloc(PNG_SIGNATURE.length);
    if (!readExact(fd, signature, 0) || !signature.equals(PNG_SIGNATURE)) {
      return null;
    }

    const header = Buffer.alloc(8); // 4-byte length + 4-byte type
    let offset = PNG_SIGNATURE.length;
    let chunksBeforeIdat = 0;
    let textBytesBeforeIdat = 0;

    while (stat.size - offset >= PNG_CHUNK_OVERHEAD_BYTES) {
      chunksBeforeIdat += 1;
      if (chunksBeforeIdat > MAX_PNG_CHUNKS_BEFORE_IDAT) return null;
      if (!readExact(fd, header, offset)) return null;

      const chunkLength = header.readUInt32BE(0);
      const chunkType = header.toString('ascii', 4, 8);
      const remainingAfterHeader = stat.size - (offset + header.length);

      // Four trailing CRC bytes are mandatory. Subtraction avoids allowing a
      // malicious declared length to wrap or advance beyond the real file.
      if (remainingAfterHeader < 4 || chunkLength > remainingAfterHeader - 4) {
        return null;
      }

      if (chunkType === 'IDAT' || chunkType === 'IEND') return null;

      if (chunkType === 'tEXt') {
        textBytesBeforeIdat += chunkLength;
        if (textBytesBeforeIdat > MAX_PNG_TEXT_BYTES_BEFORE_IDAT) return null;

        // PNG tEXt keywords are 1-79 bytes followed by NUL. Read only enough to
        // identify the keyword so unrelated chunks never require full payload
        // allocation.
        const prefixLength = Math.min(chunkLength, MAX_PNG_TEXT_KEYWORD_BYTES + 1);
        const prefix = Buffer.alloc(prefixLength);
        if (!readExact(fd, prefix, offset + header.length)) return null;

        const nullIndex = prefix.indexOf(0);
        if (nullIndex > 0 && nullIndex <= MAX_PNG_TEXT_KEYWORD_BYTES) {
          const keyword = prefix.toString('latin1', 0, nullIndex);
          if (keyword === 'parameters') {
            if (chunkLength > MAX_PNG_METADATA_CHUNK_BYTES) return null;

            const data = Buffer.alloc(chunkLength);
            if (!readExact(fd, data, offset + header.length)) return null;

            return parseSDText(data.toString('utf8', nullIndex + 1));
          }
        }
      }

      offset += PNG_CHUNK_OVERHEAD_BYTES + chunkLength;
    }

    return null;
  } catch {
    return null;
  } finally {
    if (fd !== null) {
      try { fs.closeSync(fd); } catch { /* ignore close failures */ }
    }
  }
}

/**
 * Parse the raw SD "parameters" text into structured parts.
 *
 * Format:
 *   <prompt text, possibly multiline, may contain BREAK>
 *   Negative prompt: <negative prompt text, possibly multiline>
 *   Steps: <value>, Sampler: <value>, ...
 */
function parseSDText(raw: string): ParsedMetadata {
  let prompt = '';
  let negativePrompt = '';
  const settings: Record<string, string> = {};

  // Find "Negative prompt:" marker
  const negIdx = raw.indexOf('Negative prompt:');

  // Find the last line that starts with "Steps:" – this marks generation params
  const stepsMatch = raw.match(/\nSteps:\s/);
  const stepsIdx = stepsMatch ? raw.indexOf(stepsMatch[0]) : -1;

  if (negIdx >= 0 && stepsIdx >= 0) {
    prompt = raw.substring(0, negIdx).trim();
    negativePrompt = raw.substring(negIdx + 'Negative prompt:'.length, stepsIdx).trim();
    const settingsStr = raw.substring(stepsIdx + 1); // skip the \n
    parseSettingsLine(settingsStr, settings);
  } else if (negIdx >= 0) {
    prompt = raw.substring(0, negIdx).trim();
    negativePrompt = raw.substring(negIdx + 'Negative prompt:'.length).trim();
  } else if (stepsIdx >= 0) {
    prompt = raw.substring(0, stepsIdx).trim();
    const settingsStr = raw.substring(stepsIdx + 1);
    parseSettingsLine(settingsStr, settings);
  } else {
    prompt = raw.trim();
  }

  return { raw, prompt, negativePrompt, settings };
}

/**
 * Parse "Steps: 50, Sampler: DPM++ 3M SDE, ..." into key/value pairs.
 * Handles quoted values that may contain commas (e.g. ADetailer prompt).
 */
function parseSettingsLine(line: string, out: Record<string, string>) {
  // Simple state-machine parser for comma-separated key: value pairs
  // that respects quoted strings
  let current = line.trim();
  while (current.length > 0) {
    const colonIdx = current.indexOf(':');
    if (colonIdx < 0) break;

    const key = current.substring(0, colonIdx).trim();
    current = current.substring(colonIdx + 1).trimStart();

    let value: string;
    if (current.startsWith('"')) {
      // Find matching closing quote
      let endQuote = current.indexOf('"', 1);
      while (endQuote >= 0 && current[endQuote - 1] === '\\') {
        endQuote = current.indexOf('"', endQuote + 1);
      }
      if (endQuote >= 0) {
        value = current.substring(1, endQuote);
        current = current.substring(endQuote + 1).replace(/^\s*,\s*/, '');
      } else {
        value = current.substring(1);
        current = '';
      }
    } else {
      // Find next comma that's followed by a key (word + colon)
      const commaMatch = current.match(/,\s*(?=[A-Za-z_ ]+:)/);
      if (commaMatch && commaMatch.index !== undefined) {
        value = current.substring(0, commaMatch.index).trim();
        current = current.substring(commaMatch.index + 1).trimStart();
      } else {
        value = current.trim();
        current = '';
      }
    }

    out[key] = value;
  }
}
