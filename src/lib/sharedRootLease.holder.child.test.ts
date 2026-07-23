import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { describe, expect, it } from 'vitest';

import { activateSharedRoot } from './sharedRootLocator';

const enabled = process.env.H25_SHARED_ROOT_HOLDER_SMOKE === '1';

function requiredPath(name: string) {
  const value = process.env[name];
  if (!value || !path.isAbsolute(value)) throw new Error(`${name} must be an absolute path.`);
  return path.resolve(value);
}

function isStrictChild(root: string, candidate: string) {
  const relative = path.relative(root, candidate);
  return Boolean(relative) && !relative.startsWith('..') && !path.isAbsolute(relative);
}

async function canonicalizePath(candidate: string) {
  let existing = path.resolve(candidate);
  const missingSegments: string[] = [];
  while (!await fs.stat(existing).then(() => true).catch(() => false)) {
    const parent = path.dirname(existing);
    if (parent === existing) throw new Error('Path has no existing ancestor.');
    missingSegments.unshift(path.basename(existing));
    existing = parent;
  }
  return path.resolve(await fs.realpath(existing), ...missingSegments);
}

describe.runIf(enabled)('H25 shared-root reader lease child', () => {
  it('holds the production reader lease until the release signal', { timeout: 25_000 }, async () => {
    const runRoot = requiredPath('H25_RUN_ROOT');
    const locatorPath = requiredPath('H25_LOCATOR_PATH');
    const legacyRoot = requiredPath('H25_LEGACY_ROOT');
    const leaseDirectory = requiredPath('H25_LEASE_DIRECTORY');
    const readyPath = requiredPath('H25_READY_PATH');
    const releasePath = requiredPath('H25_RELEASE_PATH');
    const resultPath = requiredPath('H25_RESULT_PATH');
    const tempRoot = await canonicalizePath(os.tmpdir());
    const canonicalRunRoot = await canonicalizePath(runRoot);

    expect(isStrictChild(tempRoot.toLowerCase(), canonicalRunRoot.toLowerCase())).toBe(true);
    for (const candidate of [locatorPath, legacyRoot, leaseDirectory, readyPath, releasePath, resultPath]) {
      const canonicalCandidate = await canonicalizePath(candidate);
      expect(isStrictChild(canonicalRunRoot.toLowerCase(), canonicalCandidate.toLowerCase())).toBe(true);
    }

    const activation = activateSharedRoot({ locatorPath, legacyRoot, leaseDirectory });
    expect(activation.status).not.toBe('Unavailable');
    expect(activation.lease).toBeDefined();
    await fs.writeFile(readyPath, '', { flag: 'wx' });

    const deadline = Date.now() + 20_000;
    while (Date.now() < deadline) {
      if (await fs.stat(releasePath).then(() => true).catch(() => false)) break;
      await new Promise((resolve) => setTimeout(resolve, 25));
    }
    expect(await fs.stat(releasePath).then(() => true).catch(() => false)).toBe(true);
    activation.lease?.close();
    await fs.writeFile(resultPath, `${JSON.stringify({
      ok: true,
      status: activation.status,
      sharedDataRoot: activation.root,
      released: true,
    })}\n`, { flag: 'wx' });
  });
});
