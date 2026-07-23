import { createHash } from 'crypto';
import fsSync, { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterEach, describe, expect, it } from 'vitest';

import locatorFixture from '../../contracts/shared-root-locator-v1.json';
import { acquireLocatorWriterLease, LocatorLease } from './sharedRootLease';
import {
  activateSharedRoot,
  SHARED_ROOT_LOCATOR_ENV,
  SharedRootLocatorErrorCode,
  SharedRootLocatorResult,
} from './sharedRootLocator';

interface LocatorCase {
  id: string;
  mode: string;
  legacyRoot?: string;
  expected: {
    status: SharedRootLocatorResult['status'];
    errorCode?: SharedRootLocatorErrorCode;
    root: 'data' | 'legacy' | 'none';
  };
}

const roots: string[] = [];

afterEach(async () => {
  await Promise.all(roots.splice(0).map((root) => fs.rm(root, { recursive: true, force: true })));
});

function generatedBytes(byteLength: number, prefix: string, suffix: string) {
  const prefixBytes = Buffer.from(prefix, 'utf8');
  const suffixBytes = Buffer.from(suffix, 'utf8');
  return Buffer.concat([
    prefixBytes,
    Buffer.alloc(byteLength - prefixBytes.length - suffixBytes.length, 0x61),
    suffixBytes,
  ]);
}

function findUnavailableVolumePath() {
  for (let code = 'Z'.charCodeAt(0); code >= 'D'.charCodeAt(0); code -= 1) {
    const root = `${String.fromCharCode(code)}:\\`;
    if (!path.parse(os.tmpdir()).root.toLowerCase().startsWith(root.toLowerCase()) && !fsSync.existsSync(root)) {
      return path.join(root, 'h25-unavailable', 'shared-root.v1.json');
    }
  }
  return path.join('Z:\\', 'h25-unavailable', 'shared-root.v1.json');
}

async function runCase(testCase: LocatorCase) {
  const runRoot = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-locator-contract-'));
  roots.push(runRoot);
  const dataRoot = path.join(runRoot, 'data');
  const legacyRoot = path.join(runRoot, 'legacy');
  const locatorDirectory = path.join(runRoot, 'locator');
  const leaseDirectory = path.join(runRoot, 'leases');
  const locatorPath = path.join(locatorDirectory, 'shared-root.v1.json');
  await Promise.all([
    fs.mkdir(dataRoot),
    fs.mkdir(legacyRoot),
    fs.mkdir(locatorDirectory),
  ]);

  let explicitLocatorPath: string | undefined = locatorPath;
  let environment: Readonly<Record<string, string | undefined>> = {};
  const selectedLegacyRoot = testCase.legacyRoot === 'missing'
    ? path.join(runRoot, 'missing-legacy')
    : legacyRoot;
  let heldWriter: LocatorLease | undefined;

  const writeJson = (document: unknown) => fs.writeFile(locatorPath, `${JSON.stringify(document)}\n`, 'utf8');
  const validDocument = { schemaVersion: 1, sharedDataRoot: dataRoot, unknownRoot: { keep: true } };

  switch (testCase.mode) {
    case 'missing':
      break;
    case 'valid':
      await writeJson(validDocument);
      break;
    case 'normalized-existing-root':
      await writeJson({ ...validDocument, sharedDataRoot: path.join(dataRoot, '..', path.basename(dataRoot)) });
      break;
    case 'linked-existing-root': {
      const linkedRoot = path.join(runRoot, 'data-link');
      await fs.symlink(dataRoot, linkedRoot, 'junction');
      await writeJson({ ...validDocument, sharedDataRoot: linkedRoot });
      break;
    }
    case 'same-locator-different-legacy-roots': {
      await writeJson(validDocument);
      const otherLegacy = path.join(runRoot, 'other-legacy');
      await fs.mkdir(otherLegacy);
      const first = activateSharedRoot({ legacyRoot, locatorPath, leaseDirectory });
      const second = activateSharedRoot({ legacyRoot: otherLegacy, locatorPath, leaseDirectory });
      first.lease?.close();
      second.lease?.close();
      return { result: second, runRoot, dataRoot, legacyRoot, leaseDirectory };
    }
    case 'environment-valid':
      await writeJson(validDocument);
      explicitLocatorPath = undefined;
      environment = { [SHARED_ROOT_LOCATOR_ENV]: locatorPath };
      break;
    case 'malformed':
      await fs.writeFile(locatorPath, '{', 'utf8');
      break;
    case 'future':
      await writeJson({ ...validDocument, schemaVersion: 2 });
      break;
    case 'relative-data-root':
      await writeJson({ ...validDocument, sharedDataRoot: 'relative-data' });
      break;
    case 'invalid-data-root-character':
      await writeJson({ ...validDocument, sharedDataRoot: `${dataRoot}\0invalid` });
      break;
    case 'missing-data-root':
      await writeJson({ ...validDocument, sharedDataRoot: path.join(runRoot, 'missing-data') });
      break;
    case 'file-data-root': {
      const fileRoot = path.join(runRoot, 'file-root');
      await fs.writeFile(fileRoot, 'file', 'utf8');
      await writeJson({ ...validDocument, sharedDataRoot: fileRoot });
      break;
    }
    case 'duplicate-required-field':
      await fs.writeFile(locatorPath, `{"schemaVersion":1,"schemaVersion":1,"sharedDataRoot":${JSON.stringify(dataRoot)}}`, 'utf8');
      break;
    case 'duplicate-shared-data-root':
      await fs.writeFile(locatorPath, `{"schemaVersion":1,"sharedDataRoot":${JSON.stringify(dataRoot)},"sharedDataRoot":${JSON.stringify(dataRoot)}}`, 'utf8');
      break;
    case 'locked-valid':
      await writeJson(validDocument);
      heldWriter = acquireLocatorWriterLease(leaseDirectory);
      break;
    case 'oversized': {
      const prefix = `{"schemaVersion":1,"sharedDataRoot":${JSON.stringify(dataRoot)},"padding":"`;
      await fs.writeFile(locatorPath, generatedBytes(65_537, prefix, '"}'));
      break;
    }
    case 'relative-locator-path':
      explicitLocatorPath = 'relative/shared-root.v1.json';
      break;
    case 'invalid-locator-character':
      explicitLocatorPath = `${locatorPath}\0invalid`;
      break;
    case 'environment-relative-locator':
      explicitLocatorPath = undefined;
      environment = { [SHARED_ROOT_LOCATOR_ENV]: 'relative/shared-root.v1.json' };
      break;
    case 'environment-whitespace-locator':
      explicitLocatorPath = undefined;
      environment = { [SHARED_ROOT_LOCATOR_ENV]: '   ' };
      break;
    case 'unavailable-volume-locator':
      explicitLocatorPath = findUnavailableVolumePath();
      break;
    case 'non-directory-locator-parent': {
      const fileParent = path.join(runRoot, 'file-parent');
      await fs.writeFile(fileParent, 'file', 'utf8');
      explicitLocatorPath = path.join(fileParent, 'shared-root.v1.json');
      break;
    }
    default:
      throw new Error(`Unimplemented locator fixture mode: ${testCase.mode}`);
  }

  try {
    const result = activateSharedRoot({
      legacyRoot: selectedLegacyRoot,
      locatorPath: explicitLocatorPath,
      leaseDirectory,
      environment,
    });
    result.lease?.close();
    return { result, runRoot, dataRoot, legacyRoot, leaseDirectory };
  } finally {
    heldWriter?.close();
  }
}

describe(`generated ${locatorFixture.contractId} fixture`, () => {
  it('retains the exact authoritative 23-case snapshot', async () => {
    const bytes = await fs.readFile(path.resolve('contracts/shared-root-locator-v1.json'));
    expect(createHash('sha256').update(bytes).digest('hex'))
      .toBe('e2988a15b282f4da9c9cd2fed8925be542d227e14a16ade29025a536a2bf18d1');
    expect(locatorFixture.cases).toHaveLength(23);
  });

  it('treats a missing default leaf directory as a missing locator, not an unreadable volume', async () => {
    const runRoot = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-locator-missing-parent-'));
    roots.push(runRoot);
    const legacyRoot = path.join(runRoot, 'legacy');
    await fs.mkdir(legacyRoot);
    const result = activateSharedRoot({
      legacyRoot,
      locatorPath: path.join(runRoot, 'not-created', 'shared-root.v1.json'),
      leaseDirectory: path.join(runRoot, 'leases'),
    });
    result.lease?.close();
    expect(result).toMatchObject({ status: 'LegacyFallback', root: await fs.realpath(legacyRoot) });
  });

  it.each(locatorFixture.cases as LocatorCase[])('$id', async (testCase) => {
    const { result, dataRoot, legacyRoot, leaseDirectory } = await runCase(testCase);
    expect(result.status).toBe(testCase.expected.status);
    if (testCase.expected.status === 'Unavailable') {
      expect(result).toMatchObject({ root: null, errorCode: testCase.expected.errorCode });
    } else {
      const expectedRoot = await fs.realpath(
        testCase.expected.root === 'data' ? dataRoot : legacyRoot,
      );
      expect(result.root).toBe(expectedRoot);
    }

    const leasePath = path.join(leaseDirectory, 'locator.lock');
    expect((await fs.stat(leasePath)).size).toBe(0);
  });
});
