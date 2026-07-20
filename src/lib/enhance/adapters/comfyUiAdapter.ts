import crypto from 'crypto';
import fs from 'fs';
import path from 'path';
import sharp from 'sharp';
import { publishEnhancementOutput } from '../outputPublish';
import type { EnhancementAdapter } from '../types';
import {
  COMFY_ANIME_MODEL,
  COMFY_GENERAL_MODEL,
  getComfyUiConfigErrorMessage,
  getComfyUiConfigStatus,
  getWorkflowPath,
} from './comfyUiConfig';
import {
  comfyFetch,
  readComfyJson,
  readComfyResponseBuffer,
} from './comfyUiClient';

type ComfyWorkflowNode = {
  class_type?: string;
  inputs?: Record<string, unknown>;
};

type ComfyWorkflow = Record<string, ComfyWorkflowNode>;

type ComfyHistoryOutputImage = {
  filename: string;
  subfolder?: string;
  type?: string;
};

type OutputWriteDiagnostics = {
  downloadMs: number;
  postprocessMs: number;
  outputWidth?: number;
  outputHeight?: number;
  outputMP?: number;
  notes?: string[];
};

const DEFAULT_COMFY_SOURCE_BYTE_LIMIT = 256 * 1024 * 1024;
const MAX_COMFY_SOURCE_BYTE_LIMIT = 1024 * 1024 * 1024;
const MAX_COMFY_IMAGE_PIXELS = 160_000_000;
const DEFAULT_COMFY_JOB_TIMEOUT_MS = 30 * 60_000;
const MAX_COMFY_JOB_TIMEOUT_MS = 24 * 60 * 60_000;

function wait(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function elapsedMs(started: number) {
  return Math.max(0, Math.round(Date.now() - started));
}

function megapixels(width?: number, height?: number) {
  if (!width || !height) return undefined;
  return Math.round((width * height / 1_000_000) * 100) / 100;
}

function boundedPositiveInteger(value: string | undefined, fallback: number, maximum: number) {
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed <= 0) return fallback;
  return Math.min(parsed, maximum);
}

function getComfySourceByteLimit() {
  return boundedPositiveInteger(
    process.env.PVU_COMFY_MAX_SOURCE_BYTES,
    DEFAULT_COMFY_SOURCE_BYTE_LIMIT,
    MAX_COMFY_SOURCE_BYTE_LIMIT,
  );
}

function resolveComfyJobTimeoutMs(value = process.env.PVU_COMFY_TIMEOUT_MS) {
  return Math.max(
    30_000,
    boundedPositiveInteger(value, DEFAULT_COMFY_JOB_TIMEOUT_MS, MAX_COMFY_JOB_TIMEOUT_MS),
  );
}

function createTemporaryOutputPath(outputPath: string) {
  return path.join(
    path.dirname(outputPath),
    `.${path.basename(outputPath)}.${process.pid}.${Date.now()}.${crypto.randomBytes(6).toString('hex')}.tmp`,
  );
}

async function readSourceImageForUpload(sourcePath: string) {
  const maximumBytes = getComfySourceByteLimit();
  const stat = await fs.promises.stat(sourcePath);
  if (!stat.isFile()) throw new Error('ComfyUI source must be a regular file.');
  if (stat.size > maximumBytes) {
    throw new Error(`ComfyUI source exceeded the ${maximumBytes}-byte limit.`);
  }
  const bytes = await fs.promises.readFile(sourcePath);
  // Re-check after the read so a concurrently replaced or extended source cannot
  // bypass the pre-read size gate.
  if (bytes.byteLength > maximumBytes) {
    throw new Error(`ComfyUI source exceeded the ${maximumBytes}-byte limit.`);
  }
  return bytes;
}

async function readWorkflow(): Promise<ComfyWorkflow> {
  const workflowPath = getWorkflowPath();
  let raw: string;
  try {
    raw = await fs.promises.readFile(workflowPath, 'utf8');
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      throw new Error(getComfyUiConfigErrorMessage(getComfyUiConfigStatus()));
    }
    throw error;
  }

  const parsed = JSON.parse(raw) as unknown;
  if (parsed && typeof parsed === 'object' && 'prompt' in parsed) {
    return (parsed as { prompt: ComfyWorkflow }).prompt;
  }
  return parsed as ComfyWorkflow;
}

function workflowImageName(uploaded: { name: string; subfolder?: string }) {
  return uploaded.subfolder ? `${uploaded.subfolder}/${uploaded.name}` : uploaded.name;
}

function patchWorkflow(workflow: ComfyWorkflow, imageName: string, scale: number) {
  let loadImagePatched = 0;
  let scalePatched = 0;
  for (const node of Object.values(workflow)) {
    if (!node.inputs) node.inputs = {};
    const classType = node.class_type || '';
    if (classType === 'LoadImage') {
      node.inputs.image = imageName;
      loadImagePatched += 1;
    }
    for (const key of ['scale', 'upscale_by', 'factor']) {
      if (key in node.inputs && typeof node.inputs[key] === 'number') {
        node.inputs[key] = scale;
        scalePatched += 1;
      }
    }
  }
  if (loadImagePatched === 0) {
    throw new Error('ComfyUI workflow has no LoadImage/image input node to receive the source image.');
  }
  return { scalePatched };
}

function getUpscaleModelName(modelFamily: string | undefined) {
  return modelFamily === 'anime' ? COMFY_ANIME_MODEL : COMFY_GENERAL_MODEL;
}

function patchUpscaleModel(workflow: ComfyWorkflow, modelName: string) {
  let modelPatched = 0;
  for (const node of Object.values(workflow)) {
    if (!node.inputs) node.inputs = {};
    if (node.class_type === 'UpscaleModelLoader') {
      node.inputs.model_name = modelName;
      modelPatched += 1;
    }
  }
  if (modelPatched === 0) {
    throw new Error('ComfyUI workflow has no UpscaleModelLoader/model_name input node to receive the upscale model.');
  }
}

function patchSaveImagePrefix(workflow: ComfyWorkflow, prefix: string) {
  let prefixPatched = 0;
  for (const node of Object.values(workflow)) {
    if (!node.inputs) node.inputs = {};
    if (node.class_type === 'SaveImage') {
      node.inputs.filename_prefix = prefix;
      prefixPatched += 1;
    }
  }
  return { prefixPatched };
}

export const comfyUiAdapterTestHooks = {
  patchWorkflow,
  patchUpscaleModel,
  patchSaveImagePrefix,
  getComfySourceByteLimit,
  resolveComfyJobTimeoutMs,
  readSourceImageForUpload,
};

async function uploadImage(sourcePath: string, jobId: string) {
  const bytes = await readSourceImageForUpload(sourcePath);
  const form = new FormData();
  form.append('image', new Blob([bytes]), `pvu_${jobId}_${path.basename(sourcePath)}`);
  form.append('overwrite', 'true');
  const response = await comfyFetch('/upload/image', {
    method: 'POST',
    body: form,
  });
  return await readComfyJson<{ name: string; subfolder?: string; type?: string }>(response);
}

async function queuePrompt(workflow: ComfyWorkflow, clientId: string) {
  const response = await comfyFetch('/prompt', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prompt: workflow, client_id: clientId }),
  });
  const data = await readComfyJson<{ prompt_id?: string }>(response);
  if (!data.prompt_id) throw new Error('ComfyUI did not return a prompt_id.');
  return data.prompt_id;
}

async function getHistory(promptId: string) {
  const response = await comfyFetch(`/history/${encodeURIComponent(promptId)}`);
  return await readComfyJson<Record<string, {
    outputs?: Record<string, {
      images?: ComfyHistoryOutputImage[];
    }>;
    status?: {
      status_str?: string;
      completed?: boolean;
      messages?: unknown[];
    };
  }>>(response);
}

function findOutputImage(history: Awaited<ReturnType<typeof getHistory>>, promptId: string) {
  const entry = history[promptId];
  if (!entry) return null;
  for (const output of Object.values(entry.outputs || {})) {
    const image = output.images?.[0];
    if (image?.filename) return image;
  }
  return null;
}

async function downloadOutput(
  image: ComfyHistoryOutputImage,
  outputPath: string,
  outputFormat: 'png' | 'webp' | 'jpg',
  sourcePath: string,
  scale: number,
): Promise<OutputWriteDiagnostics> {
  const downloadStarted = Date.now();
  const params = new URLSearchParams({
    filename: image.filename,
    subfolder: image.subfolder || '',
    type: image.type || 'output',
  });
  const response = await comfyFetch(`/view?${params.toString()}`);
  const bytes = await readComfyResponseBuffer(response);
  const downloadMs = elapsedMs(downloadStarted);
  const postprocessStarted = Date.now();
  await fs.promises.mkdir(path.dirname(outputPath), { recursive: true });

  const sharpOptions = { failOn: 'none' as const, limitInputPixels: MAX_COMFY_IMAGE_PIXELS };
  let pipeline = sharp(bytes, sharpOptions);
  const notes: string[] = [];
  const sourceMeta = await sharp(sourcePath, sharpOptions).metadata();
  let outputMeta = await sharp(bytes, sharpOptions).metadata();
  if (sourceMeta.width && sourceMeta.height && Number.isFinite(scale)) {
    const targetWidth = Math.max(1, Math.round(sourceMeta.width * scale));
    const targetHeight = Math.max(1, Math.round(sourceMeta.height * scale));
    if (targetWidth * targetHeight > MAX_COMFY_IMAGE_PIXELS) {
      throw new Error(`ComfyUI target exceeds the ${MAX_COMFY_IMAGE_PIXELS}-pixel safety limit.`);
    }
    if (outputMeta.width !== targetWidth || outputMeta.height !== targetHeight) {
      pipeline = pipeline.resize(targetWidth, targetHeight, {
        fit: 'fill',
        kernel: sharp.kernel.lanczos3,
      });
      notes.push(`resized Comfy output ${outputMeta.width || '?'}x${outputMeta.height || '?'} to requested ${targetWidth}x${targetHeight}`);
      outputMeta = { ...outputMeta, width: targetWidth, height: targetHeight };
    }
  }

  const temporaryOutputPath = createTemporaryOutputPath(outputPath);
  try {
    if (outputFormat === 'png' && outputMeta.format === 'png' && notes.length === 0) {
      await fs.promises.writeFile(temporaryOutputPath, bytes);
      notes.push('copied Comfy PNG output without Sharp re-encode');
    } else {
      if (outputFormat === 'webp') {
        pipeline = pipeline.webp({ quality: 94 });
      } else if (outputFormat === 'jpg') {
        pipeline = pipeline.jpeg({ quality: 95, mozjpeg: true });
      } else {
        pipeline = pipeline.png();
      }
      await pipeline.toFile(temporaryOutputPath);
    }

    const publishMethod = await publishEnhancementOutput(temporaryOutputPath, outputPath);
    if (publishMethod !== 'rename') {
      notes.push('Windows file locking required safe copy publication.');
    }
    if (publishMethod === 'copy-with-stale-temporary') {
      notes.push('Output was published, but temporary cleanup remains pending.');
    }

    return {
      downloadMs,
      postprocessMs: elapsedMs(postprocessStarted),
      outputWidth: outputMeta.width,
      outputHeight: outputMeta.height,
      outputMP: megapixels(outputMeta.width, outputMeta.height),
      notes,
    };
  } finally {
    await fs.promises.rm(temporaryOutputPath, { force: true }).catch(() => {});
  }
}

export const comfyUiAdapter: EnhancementAdapter = {
  id: 'comfyui',
  label: 'ComfyUI AI upscale',
  async run(job, context) {
    const totalStarted = Date.now();
    await fs.promises.mkdir(path.dirname(context.outputPath), { recursive: true });
    await context.updateProgress(5);
    const workflow = await readWorkflow();
    if (await context.isCancelRequested()) throw new Error('Job canceled');

    const sourceMeta = await sharp(job.sourcePath, {
      failOn: 'none',
      limitInputPixels: MAX_COMFY_IMAGE_PIXELS,
    }).metadata();
    const sourceWidth = sourceMeta.width;
    const sourceHeight = sourceMeta.height;
    const nativeScale = 4;
    const targetWidth = sourceWidth ? Math.round(sourceWidth * context.preset.scale) : undefined;
    const targetHeight = sourceHeight ? Math.round(sourceHeight * context.preset.scale) : undefined;
    if (targetWidth && targetHeight && targetWidth * targetHeight > MAX_COMFY_IMAGE_PIXELS) {
      throw new Error(`ComfyUI target exceeds the ${MAX_COMFY_IMAGE_PIXELS}-pixel safety limit.`);
    }
    const diagnosticsNotes = [
      ...(context.preset.scale !== nativeScale
        ? [`Comfy Real-ESRGAN model is native ${nativeScale}x; requested ${context.preset.scale}x requires post-resize.`]
        : []),
      ...(context.preset.outputFormat === 'png' && targetWidth && targetHeight && (megapixels(targetWidth, targetHeight) || 0) > 60
        ? ['Large PNG target may spend noticeable time in encode/write after AI completes.']
        : []),
    ];
    await context.updateDiagnostics({
      sourceWidth,
      sourceHeight,
      sourceMP: megapixels(sourceWidth, sourceHeight),
      requestedScale: context.preset.scale,
      nativeScale,
      targetWidth,
      targetHeight,
      targetMP: megapixels(targetWidth, targetHeight),
      notes: diagnosticsNotes,
    });

    const uploadStarted = Date.now();
    const uploaded = await uploadImage(job.sourcePath, job.id);
    await context.updateDiagnostics({ uploadMs: elapsedMs(uploadStarted) });
    await context.updateProgress(20);
    patchWorkflow(workflow, workflowImageName(uploaded), context.preset.scale);
    patchUpscaleModel(workflow, getUpscaleModelName(context.preset.modelFamily));
    patchSaveImagePrefix(workflow, `pvu_${job.id}`);

    const promptId = await queuePrompt(workflow, `pvu-${job.id}`);
    await context.updateExternalPromptId(promptId);
    await context.updateProgress(30);

    const started = Date.now();
    const timeoutMs = resolveComfyJobTimeoutMs();
    while (Date.now() - started < timeoutMs) {
      if (await context.isCancelRequested()) throw new Error('Job canceled');
      const history = await getHistory(promptId);
      const outputImage = findOutputImage(history, promptId);
      if (outputImage) {
        await context.updateDiagnostics({ comfyWaitMs: elapsedMs(started) });
        await context.updateProgress(80);
        const outputDiagnostics = await downloadOutput(
          outputImage,
          context.outputPath,
          context.preset.outputFormat,
          job.sourcePath,
          context.preset.scale,
        );
        await context.updateDiagnostics({
          ...outputDiagnostics,
          notes: [
            ...diagnosticsNotes,
            ...(outputDiagnostics.notes || []),
          ],
          totalMs: elapsedMs(totalStarted),
        });
        await context.updateProgress(100);
        return { outputPath: context.outputPath };
      }
      const progressWindowMs = 10 * 60_000;
      const elapsedRatio = Math.min(1, (Date.now() - started) / progressWindowMs);
      await context.updateProgress(30 + elapsedRatio * 35);
      await wait(1000);
    }

    throw new Error(`ComfyUI job exceeded the configured timeout (${Math.round(timeoutMs / 1000)} seconds).`);
  },
};
