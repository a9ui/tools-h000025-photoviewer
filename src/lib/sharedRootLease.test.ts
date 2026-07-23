import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  acquireLocatorReaderLease,
  acquireLocatorWriterLease,
  LOCATOR_LEASE_DIRECTORY_NAME,
  LocatorLeaseError,
  resolveLocatorLeasePath,
} from './sharedRootLease';

const roots: string[] = [];

afterEach(async () => {
  await Promise.all(roots.splice(0).map((root) => fs.rm(root, { recursive: true, force: true })));
});

async function makeLeaseDirectory() {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-locator-lease-'));
  roots.push(root);
  return path.join(root, 'leases');
}

describe('Aibos shared-root locator Win32 lease', () => {
  it('allows readers together, blocks a writer, and admits the writer after release', async () => {
    const directory = await makeLeaseDirectory();
    const first = acquireLocatorReaderLease(directory);
    const second = acquireLocatorReaderLease(directory);

    expect(() => acquireLocatorWriterLease(directory)).toThrowError(
      expect.objectContaining<Partial<LocatorLeaseError>>({ code: 'locator-lease-busy', win32Error: 32 }),
    );

    first.close();
    expect(() => acquireLocatorWriterLease(directory)).toThrowError(
      expect.objectContaining<Partial<LocatorLeaseError>>({ code: 'locator-lease-busy' }),
    );

    second.close();
    const writer = acquireLocatorWriterLease(directory);
    expect((await fs.stat(writer.path)).size).toBe(0);
    writer.close();
    writer.close();
    expect(await fs.readFile(path.join(directory, 'locator.lock'))).toHaveLength(0);
  });

  it('blocks readers while the exclusive writer handle is alive', async () => {
    const directory = await makeLeaseDirectory();
    const writer = acquireLocatorWriterLease(directory);
    expect(() => acquireLocatorReaderLease(directory)).toThrowError(
      expect.objectContaining<Partial<LocatorLeaseError>>({ code: 'locator-lease-busy' }),
    );
    writer.close();
  });

  it('reports the protocol-global production path without opening it', () => {
    const leasePath = resolveLocatorLeasePath();
    expect(leasePath).toBe(path.join(os.tmpdir(), LOCATOR_LEASE_DIRECTORY_NAME, 'locator.lock'));
  });

  it('rejects an override directory that resolves outside OS TEMP', () => {
    expect(() => resolveLocatorLeasePath(path.parse(os.tmpdir()).root)).toThrowError(
      expect.objectContaining<Partial<LocatorLeaseError>>({ code: 'locator-lease-path-invalid' }),
    );
  });

  it('rejects a junction escape before creating a child through it', async () => {
    const root = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-locator-junction-'));
    roots.push(root);
    const allowedRoot = path.join(root, 'allowed');
    const outsideRoot = path.join(root, 'outside');
    const junction = path.join(allowedRoot, 'escape');
    const escapedChild = path.join(outsideRoot, 'must-not-exist');
    await Promise.all([fs.mkdir(allowedRoot), fs.mkdir(outsideRoot)]);
    await fs.symlink(outsideRoot, junction, 'junction');
    const tempRoot = vi.spyOn(os, 'tmpdir').mockReturnValue(allowedRoot);
    try {
      expect(() => resolveLocatorLeasePath(path.join(junction, 'must-not-exist'))).toThrowError(
        expect.objectContaining<Partial<LocatorLeaseError>>({ code: 'locator-lease-path-invalid' }),
      );
      await expect(fs.stat(escapedChild)).rejects.toMatchObject({ code: 'ENOENT' });
    } finally {
      tempRoot.mockRestore();
    }
  });

  it('rejects an in-TEMP junction that would split the protocol-global lock identity', async () => {
    const root = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-locator-lease-split-'));
    roots.push(root);
    const actual = path.join(root, 'actual');
    const redirected = path.join(root, 'leases');
    await fs.mkdir(actual);
    await fs.symlink(actual, redirected, 'junction');

    expect(() => resolveLocatorLeasePath(redirected)).toThrowError(
      expect.objectContaining<Partial<LocatorLeaseError>>({ code: 'locator-lease-path-invalid' }),
    );
    await expect(fs.stat(path.join(actual, 'locator.lock'))).rejects.toMatchObject({ code: 'ENOENT' });
  });
});
