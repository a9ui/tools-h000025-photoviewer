import fs from 'fs';
import os from 'os';
import path from 'path';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { comfyUiAdapterTestHooks } from './comfyUiAdapter';

const createdRoots: string[] = [];

afterEach(() => {
  vi.unstubAllEnvs();
  for (const root of createdRoots.splice(0)) {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

describe('comfy UI adapter workflow patching', () => {
  it('patches only source LoadImage and model/prefix nodes without breaking graph links', () => {
    const workflow = {
      '1': {
        class_type: 'LoadImage',
        inputs: { image: 'placeholder.png' },
      },
      '2': {
        class_type: 'UpscaleModelLoader',
        inputs: { model_name: 'old.pth' },
      },
      '3': {
        class_type: 'ImageUpscaleWithModel',
        inputs: {
          upscale_model: ['2', 0],
          image: ['1', 0],
        },
      },
      '4': {
        class_type: 'SaveImage',
        inputs: {
          images: ['3', 0],
          filename_prefix: 'old_prefix',
        },
      },
    };

    comfyUiAdapterTestHooks.patchWorkflow(workflow, 'uploaded/source.png', 2);
    comfyUiAdapterTestHooks.patchUpscaleModel(workflow, 'RealESRGAN_x4plus_anime_6B.pth');
    comfyUiAdapterTestHooks.patchSaveImagePrefix(workflow, 'pvu_job');

    expect(workflow['1'].inputs.image).toBe('uploaded/source.png');
    expect(workflow['2'].inputs.model_name).toBe('RealESRGAN_x4plus_anime_6B.pth');
    expect(workflow['3'].inputs.image).toEqual(['1', 0]);
    expect(workflow['3'].inputs.upscale_model).toEqual(['2', 0]);
    expect(workflow['4'].inputs.images).toEqual(['3', 0]);
    expect(workflow['4'].inputs.filename_prefix).toBe('pvu_job');
  });

  it('uses a finite job timeout and caps an excessive configured value', () => {
    expect(comfyUiAdapterTestHooks.resolveComfyJobTimeoutMs(undefined)).toBe(30 * 60_000);
    expect(comfyUiAdapterTestHooks.resolveComfyJobTimeoutMs('1')).toBe(30_000);
    expect(comfyUiAdapterTestHooks.resolveComfyJobTimeoutMs(String(48 * 60 * 60_000))).toBe(24 * 60 * 60_000);
  });

  it('rejects a source that exceeds the configured upload byte limit', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-comfy-source-'));
    createdRoots.push(root);
    const target = path.join(root, 'source.png');
    fs.writeFileSync(target, Buffer.from('12345'));
    vi.stubEnv('PVU_COMFY_MAX_SOURCE_BYTES', '4');

    await expect(comfyUiAdapterTestHooks.readSourceImageForUpload(target))
      .rejects.toThrow('4-byte limit');
  });

  it('reads a bounded source when it fits the configured upload limit', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'photoviewer-comfy-source-'));
    createdRoots.push(root);
    const target = path.join(root, 'source.png');
    fs.writeFileSync(target, Buffer.from('1234'));
    vi.stubEnv('PVU_COMFY_MAX_SOURCE_BYTES', '4');

    await expect(comfyUiAdapterTestHooks.readSourceImageForUpload(target))
      .resolves.toEqual(Buffer.from('1234'));
  });
});
