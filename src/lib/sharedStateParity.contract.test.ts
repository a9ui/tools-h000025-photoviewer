import { createHash } from 'crypto';
import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterEach, describe, expect, it } from 'vitest';

import parityFixture from '../../contracts/parity-v1.json';
import { GET as getRecent, PUT as putRecent } from '../app/api/recent-folders/route';
import { GET as getSettings, PUT as putSettings } from '../app/api/settings/route';
import { type AlbumMutation, mutateAlbums, readAlbums } from './albums';
import { selectRecentFolderSet } from './recentFolders';
import {
  mutateSearchHistory,
  normalizeSearchHistoryQuery,
  readSearchHistory,
  searchHistoryComparisonKey,
  type SearchHistoryMutation,
} from './searchHistory';

interface InitialState extends Record<string, unknown> {
  mode: string;
  document?: unknown;
  text?: string;
  base64?: string;
  byteLength?: number;
  prefix?: string;
  suffix?: string;
  fillByte?: number;
}

interface ContractCase extends Record<string, unknown> {
  id: string;
  initial?: InitialState;
  expected: Record<string, unknown>;
  operations?: Array<Record<string, unknown>>;
}

interface Contract extends Record<string, unknown> {
  id: string;
  cases: ContractCase[];
}

const EXPECTED_FIXTURE_SHA256 = 'b77844c6a2b24f5866cc0b392e08a21781198bf3c4a285a9b0774359233ce481';
const contracts = parityFixture.contracts as unknown as Contract[];
const roots: string[] = [];
const previousSettingsPath = process.env.PVU_SETTINGS_PATH;
const previousRecentPath = process.env.PVU_RECENT_FOLDERS_PATH;

afterEach(async () => {
  if (previousSettingsPath === undefined) delete process.env.PVU_SETTINGS_PATH;
  else process.env.PVU_SETTINGS_PATH = previousSettingsPath;
  if (previousRecentPath === undefined) delete process.env.PVU_RECENT_FOLDERS_PATH;
  else process.env.PVU_RECENT_FOLDERS_PATH = previousRecentPath;
  await Promise.all(roots.splice(0).map((root) => fs.rm(root, { recursive: true, force: true })));
});

function contract(id: string) {
  const selected = contracts.find((candidate) => candidate.id === id);
  if (!selected) throw new Error(`Missing parity contract ${id}.`);
  return selected;
}

function substituteRoot<T>(value: T, root: string): T {
  if (typeof value === 'string') return value.replaceAll('${ROOT}', root) as T;
  if (Array.isArray(value)) return value.map((item) => substituteRoot(item, root)) as T;
  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).map(([key, item]) => [key, substituteRoot(item, root)]),
    ) as T;
  }
  return value;
}

function generatedBytes(initial: InitialState) {
  const byteLength = initial.byteLength ?? 0;
  const prefix = Buffer.from(initial.prefix ?? '', 'utf8');
  const suffix = Buffer.from(initial.suffix ?? '', 'utf8');
  return Buffer.concat([
    prefix,
    Buffer.alloc(byteLength - prefix.length - suffix.length, initial.fillByte ?? 0x61),
    suffix,
  ]);
}

async function initialize(target: string, initial: InitialState | undefined, root: string) {
  if (!initial || initial.mode === 'missing') return;
  await fs.mkdir(path.dirname(target), { recursive: true });
  if (initial.mode === 'json') {
    await fs.writeFile(target, `${JSON.stringify(substituteRoot(initial.document, root))}\n`, 'utf8');
  } else if (initial.mode === 'raw') {
    await fs.writeFile(target, substituteRoot(initial.text ?? '', root), 'utf8');
  } else if (initial.mode === 'bytes-base64') {
    await fs.writeFile(target, Buffer.from(initial.base64 ?? '', 'base64'));
  } else if (initial.mode === 'generated-utf8') {
    await fs.writeFile(target, generatedBytes(initial));
  } else {
    throw new Error(`Unsupported fixture initialization mode: ${initial.mode}`);
  }
}

async function snapshot(target: string) {
  try {
    return await fs.readFile(target);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return null;
    throw error;
  }
}

function bytesEqual(first: Buffer | null, second: Buffer | null) {
  if (first === null || second === null) return first === second;
  return first.equals(second);
}

async function exists(target: string) {
  return fs.stat(target).then(() => true).catch(() => false);
}

function request(url: string, body: unknown) {
  return new Request(url, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
}

describe('Aibos generated parity-v1 snapshot', () => {
  it('retains the exact authoritative fixture and 6 IDs / 33 cases', async () => {
    const bytes = await fs.readFile(path.resolve('contracts/parity-v1.json'));
    expect(createHash('sha256').update(bytes).digest('hex')).toBe(EXPECTED_FIXTURE_SHA256);
    expect(contracts).toHaveLength(6);
    expect(contracts.reduce((count, item) => count + item.cases.length, 0)).toBe(33);
  });
});

describe('PV-SH-001 search identity', () => {
  for (const testCase of contract('PV-SH-001').cases) {
    it(testCase.id, () => {
      const samples = testCase.samples as Array<Record<string, string>>;
      for (const sample of samples) {
        expect(normalizeSearchHistoryQuery(sample.input)).toBe(sample.normalized);
        expect(searchHistoryComparisonKey(sample.input)).toBe(sample.comparisonKey);
      }
    });
  }
});

describe('PV-SH-002 search document', () => {
  for (const testCase of contract('PV-SH-002').cases) {
    it(testCase.id, async () => {
      const root = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-parity-search-'));
      roots.push(root);
      const target = path.join(root, 'search-history.json');
      await initialize(target, testCase.initial, root);
      const before = await snapshot(target);
      const initial = await readSearchHistory(target);
      const expected = testCase.expected;
      expect(initial.ok).toBe(expected.initialSupported);
      expect(initial.malformed).toBe(expected.initialMalformed);
      expect(initial.futureVersion).toBe(expected.initialFutureVersion);

      const operations = [...(testCase.operations ?? [])];
      const generated = testCase.generatedCommits as Record<string, unknown> | undefined;
      if (generated) {
        const count = Number(generated.count);
        const pad = Number(generated.pad);
        for (let index = 0; index < count; index += 1) {
          operations.push({ action: 'commit', query: `${generated.prefix}${String(index).padStart(pad, '0')}` });
        }
      }
      const statuses: string[] = [];
      for (const operation of operations) {
        const result = await mutateSearchHistory(target, operation as unknown as SearchHistoryMutation);
        statuses.push(result.ok ? 'Succeeded' : 'Protected');
      }
      if (Array.isArray(expected.statuses)) expect(statuses).toEqual(expected.statuses);
      else if (expected.statuses) {
        const expectedStatuses = expected.statuses as Record<string, unknown>;
        expect(statuses).toHaveLength(Number(expectedStatuses.count));
        expect(new Set(statuses)).toEqual(new Set([expectedStatuses.all]));
      }

      const final = await readSearchHistory(target);
      expect(final.ok).toBe(expected.finalSupported);
      expect(final.malformed).toBe(expected.finalMalformed);
      expect(final.futureVersion).toBe(expected.finalFutureVersion);
      if (expected.entries) expect(final.entries).toEqual(expected.entries);
      const entryWindow = expected.entryWindow as Record<string, unknown> | undefined;
      if (entryWindow) {
        expect(final.entries).toHaveLength(Number(entryWindow.count));
        expect(final.entries[0]).toBe(entryWindow.first);
        expect(final.entries.at(-1)).toBe(entryWindow.last);
      }
      expect(await exists(target)).toBe(expected.fileExists);
      expect(bytesEqual(before, await snapshot(target))).toBe(expected.bytesUnchanged);
      if (final.ok && expected.unknownRoot) {
        expect(final.document).toMatchObject(expected.unknownRoot);
      }
    });
  }
});

describe('PV-ALB-001 album document', () => {
  for (const testCase of contract('PV-ALB-001').cases) {
    it(testCase.id, async () => {
      const root = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-parity-album-'));
      roots.push(root);
      const target = path.join(root, 'albums.json');
      await initialize(target, testCase.initial, root);
      const before = await snapshot(target);
      const initial = await readAlbums(target);
      const afterRead = await snapshot(target);
      const expected = testCase.expected;
      expect(initial.ok).toBe(expected.initialSupported);
      expect(initial.exists).toBe(expected.initialExists);
      expect(initial.malformed).toBe(expected.initialMalformed);
      expect(initial.futureVersion).toBe(expected.initialFutureVersion);
      expect(initial.ok ? initial.document.revision : null).toBe(expected.initialRevision);
      expect(initial.ok ? initial.document.albums.length : null).toBe(expected.initialAlbumCount);
      expect(bytesEqual(before, afterRead)).toBe(expected.bytesUnchangedAfterRead);

      const statuses: string[] = [];
      for (const rawOperation of testCase.operations ?? []) {
        const operation = substituteRoot(rawOperation, root) as unknown as AlbumMutation;
        const result = await mutateAlbums(target, operation);
        statuses.push(result.conflict ? 'Conflict' : result.ok ? 'Succeeded' : 'Protected');
      }
      expect(statuses).toEqual(expected.statuses);
      const final = await readAlbums(target);
      expect(final.ok ? final.document.revision : null).toBe(expected.finalRevision);
      expect(final.ok ? final.document.albums.length : null).toBe(expected.finalAlbumCount);
      expect(await exists(target)).toBe(expected.fileExists);
      expect(bytesEqual(before, await snapshot(target))).toBe(expected.bytesUnchangedAfterOperations);
      if (final.ok && expected.unknownRoot) expect(final.document).toMatchObject(expected.unknownRoot);
    });
  }
});

describe('PV-ALB-002 album operations', () => {
  for (const testCase of contract('PV-ALB-002').cases) {
    it(testCase.id, async () => {
      const root = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-parity-album-ops-'));
      roots.push(root);
      const target = path.join(root, 'albums.json');
      await initialize(target, testCase.initial, root);
      const initial = await readAlbums(target);
      const expected = testCase.expected;
      expect(initial.ok).toBe(expected.initialSupported);

      const statuses: string[] = [];
      const changed: boolean[] = [];
      const revisions: Array<number | null> = [];
      for (const rawOperation of testCase.operations ?? []) {
        const result = await mutateAlbums(target, substituteRoot(rawOperation, root) as unknown as AlbumMutation);
        statuses.push(result.conflict ? 'Conflict' : result.ok ? 'Succeeded' : 'Protected');
        changed.push(result.changed);
        revisions.push(result.document?.revision ?? null);
      }
      expect(statuses).toEqual(expected.statuses);
      expect(changed).toEqual(expected.changed);
      expect(revisions).toEqual(expected.revisions);

      const final = await readAlbums(target);
      expect(final.ok).toBe(true);
      if (!final.ok) throw new Error(final.error);
      const expectedAlbum = substituteRoot(expected.finalAlbum as Record<string, unknown>, root);
      const album = final.document.albums.find((candidate) => candidate.id === expectedAlbum.id);
      expect(album).toBeDefined();
      expect(album).toMatchObject({
        id: expectedAlbum.id,
        name: expectedAlbum.name,
        pinned: expectedAlbum.pinned,
        coverMemberId: expectedAlbum.coverMemberId,
        revision: expectedAlbum.revision,
      });
      expect(album?.members.map((member) => member.imagePath)).toEqual(
        (expectedAlbum.memberPaths as string[]).map((memberPath) => path.resolve(memberPath)),
      );
      expect(final.document).toMatchObject(expected.unknownRoot as Record<string, unknown>);
      expect(album).toMatchObject(expected.unknownAlbum as Record<string, unknown>);
      const unknownMember = expected.unknownMember as Record<string, unknown>;
      expect(album?.members.find((member) => member.id === unknownMember.memberId))
        .toMatchObject(unknownMember.fields as Record<string, unknown>);
    });
  }
});

describe('PV-SET-001 shared settings', () => {
  for (const testCase of contract('PV-SET-001').cases) {
    it(testCase.id, async () => {
      const root = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-parity-settings-'));
      roots.push(root);
      const target = path.join(root, 'settings.json');
      process.env.PVU_SETTINGS_PATH = target;
      await initialize(target, testCase.initial, root);
      const before = await snapshot(target);
      const initialResponse = await getSettings();
      const initial = await initialResponse.json();
      const expected = testCase.expected;
      expect(initial.protected).toBe(expected.initialProtected);
      const effectiveConfirm = initial.confirmBeforeDeleteAuthority === 'local'
        ? testCase.localConfirmBeforeDelete
        : initial.confirmBeforeDelete;
      expect(effectiveConfirm).toBe(expected.effectiveConfirmBeforeDelete);

      const statuses: string[] = [];
      for (const operation of testCase.operations ?? []) {
        let body: Record<string, unknown>;
        if (operation.action === 'confirm') {
          body = { confirmBeforeDelete: operation.value };
        } else {
          const dirty = operation.dirty as string[];
          body = {
            thumbnailStatusBorders: Object.fromEntries(
              dirty.map((status) => [status, operation[status]]),
            ),
          };
        }
        const response = await putSettings(request('http://127.0.0.1/api/settings', body));
        statuses.push(response.ok ? 'Succeeded' : response.status === 409 ? 'Protected' : `HTTP-${response.status}`);
      }
      expect(statuses).toEqual(expected.statuses);
      expect(await exists(target)).toBe(expected.fileExists);
      expect(bytesEqual(before, await snapshot(target))).toBe(expected.bytesUnchanged);

      const expectedFinal = expected.final as InitialState;
      const finalBytes = await snapshot(target);
      if (expectedFinal.mode === 'json') {
        expect(JSON.parse(finalBytes!.toString('utf8'))).toEqual(substituteRoot(expectedFinal.document, root));
      } else {
        expect(finalBytes).toEqual(before);
      }
    }, 15_000);
  }
});

describe('PV-REC-001 recent-folder authority', () => {
  for (const testCase of contract('PV-REC-001').cases) {
    it(testCase.id, async () => {
      const root = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-parity-recent-'));
      roots.push(root);
      const target = path.join(root, 'recent-folders.json');
      process.env.PVU_RECENT_FOLDERS_PATH = target;
      await initialize(target, testCase.initial, root);
      const before = await snapshot(target);
      const readResponse = await getRecent();
      const read = await readResponse.json();
      const expected = testCase.expected;
      const localFolderSet = substituteRoot(testCase.localFolderSet as string[], root);
      expect(read.ok).toBe(expected.readOk);
      expect(read.exists).toBe(expected.exists);
      expect(selectRecentFolderSet(
        Boolean(read.ok),
        Boolean(read.exists),
        read.recent.lastFolderSet,
        localFolderSet,
      )).toEqual(substituteRoot(expected.selectedFolderSet as string[], root));

      const statuses: string[] = [];
      for (const operation of testCase.operations ?? []) {
        const folder = substituteRoot(String(operation.folder), root);
        const response = await putRecent(request('http://127.0.0.1/api/recent-folders', {
          lastDirSet: folder,
          recentDirs: [folder],
        }));
        statuses.push(response.ok ? 'Succeeded' : response.status === 409 ? 'Protected' : `HTTP-${response.status}`);
      }
      if (expected.statuses) expect(statuses).toEqual(expected.statuses);
      expect(await exists(target)).toBe(expected.fileExists);
      expect(bytesEqual(before, await snapshot(target))).toBe(expected.bytesUnchanged);

      const finalBytes = await snapshot(target);
      if (expected.canonicalUtf8WithoutBom) {
        expect(finalBytes?.subarray(0, 3)).not.toEqual(Buffer.from([0xef, 0xbb, 0xbf]));
      }
      if (expected.unknownRoot) {
        expect(JSON.parse(finalBytes!.toString('utf8'))).toMatchObject(expected.unknownRoot);
      }
    }, 15_000);
  }
});
