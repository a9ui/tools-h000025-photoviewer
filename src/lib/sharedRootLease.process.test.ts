import { type ChildProcess, spawn } from 'child_process';
import fsSync, { promises as fs } from 'fs';
import os from 'os';
import path from 'path';

import { afterEach, describe, expect, it } from 'vitest';

import locatorFixture from '../../contracts/shared-root-locator-v1.json';
import { resolveLocatorLeasePath } from './sharedRootLease';
import { writeSharedRootLocator } from './sharedRootLocator';

interface RunningHolder {
  child: ChildProcess;
  readyPath: string;
  releasePath: string;
  resultPath: string;
  output: () => string;
}

interface MatrixRun {
  root: string;
  locatorPath: string;
  legacyRoot: string;
  dataRoot: string;
  replacementRoot: string;
  leaseDirectory: string;
}

const roots: string[] = [];
const children = new Set<ChildProcess>();
const aibosWpfDll = process.env.AIBOS_WPF_DLL;

afterEach(async () => {
  const running = [...children];
  for (const child of running) child.kill();
  await Promise.all(running.map((child) => child.exitCode !== null
    ? Promise.resolve()
    : new Promise<void>((resolve) => child.once('exit', () => resolve()))));
  children.clear();
  await Promise.all(roots.splice(0).map((root) => fs.rm(root, {
    recursive: true,
    force: true,
    maxRetries: 5,
    retryDelay: 50,
  })));
});

async function makeRun(mode: 'create' | 'replace'): Promise<MatrixRun> {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'h25-lease-matrix-'));
  roots.push(root);
  const locatorDirectory = path.join(root, 'locator');
  const locatorPath = path.join(locatorDirectory, 'shared-root.v1.json');
  const legacyRoot = path.join(root, 'legacy');
  const dataRoot = path.join(root, 'data');
  const replacementRoot = path.join(root, 'replacement');
  const leaseDirectory = path.join(root, 'leases');
  await Promise.all([
    fs.mkdir(locatorDirectory),
    fs.mkdir(legacyRoot),
    fs.mkdir(dataRoot),
    fs.mkdir(replacementRoot),
  ]);
  if (mode === 'replace') {
    await fs.writeFile(locatorPath, `${JSON.stringify({ schemaVersion: 1, sharedDataRoot: dataRoot })}\n`, 'utf8');
  }
  return { root, locatorPath, legacyRoot, dataRoot, replacementRoot, leaseDirectory };
}

function collect(child: ChildProcess) {
  let output = '';
  child.stdout?.on('data', (chunk) => { output += chunk.toString(); });
  child.stderr?.on('data', (chunk) => { output += chunk.toString(); });
  return () => output;
}

async function waitForFile(holder: RunningHolder, timeoutMs = 12_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await fs.stat(holder.readyPath).then(() => true).catch(() => false)) return;
    if (holder.child.exitCode !== null) {
      throw new Error(`Lease holder exited before ready (${holder.child.exitCode}).\n${holder.output()}`);
    }
    await new Promise((resolve) => setTimeout(resolve, 25));
  }
  throw new Error(`Lease holder did not become ready.\n${holder.output()}`);
}

async function waitForExit(holder: RunningHolder, timeoutMs = 12_000) {
  if (holder.child.exitCode !== null) return holder.child.exitCode;
  return new Promise<number | null>((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`Lease holder did not exit.\n${holder.output()}`)), timeoutMs);
    holder.child.once('exit', (code) => {
      clearTimeout(timer);
      resolve(code);
    });
  });
}

async function release(holder: RunningHolder) {
  await fs.writeFile(holder.releasePath, '', { flag: 'wx' });
  expect(await waitForExit(holder)).toBe(0);
  children.delete(holder.child);
  return JSON.parse(await fs.readFile(holder.resultPath, 'utf8')) as Record<string, unknown>;
}

function startH25Holder(run: MatrixRun, name: string): RunningHolder {
  const readyPath = path.join(run.root, `${name}-ready`);
  const releasePath = path.join(run.root, `${name}-release`);
  const resultPath = path.join(run.root, `${name}-result.json`);
  const child = spawn(process.execPath, [
    path.resolve('node_modules/vitest/vitest.mjs'),
    'run',
    path.resolve('src/lib/sharedRootLease.holder.child.test.ts'),
    '--pool=forks',
    '--maxWorkers=1',
  ], {
    cwd: process.cwd(),
    env: {
      ...process.env,
      H25_SHARED_ROOT_HOLDER_SMOKE: '1',
      H25_RUN_ROOT: run.root,
      H25_LOCATOR_PATH: run.locatorPath,
      H25_LEGACY_ROOT: run.legacyRoot,
      H25_LEASE_DIRECTORY: run.leaseDirectory,
      H25_READY_PATH: readyPath,
      H25_RELEASE_PATH: releasePath,
      H25_RESULT_PATH: resultPath,
    },
    stdio: ['ignore', 'pipe', 'pipe'],
    windowsHide: true,
  });
  children.add(child);
  return { child, readyPath, releasePath, resultPath, output: collect(child) };
}

function startWpfHolder(run: MatrixRun, name: string): RunningHolder {
  if (!aibosWpfDll) throw new Error('AIBOS_WPF_DLL is required.');
  const readyPath = path.join(run.root, `${name}-ready`);
  const releasePath = path.join(run.root, `${name}-release`);
  const resultPath = path.join(run.root, `${name}-result.json`);
  const child = spawn('dotnet', [
    aibosWpfDll,
    '--shared-root-lease-holder-smoke', resultPath,
    '--temp-root', run.root,
    '--locator-path', run.locatorPath,
    '--legacy-root', run.legacyRoot,
    '--ready-path', readyPath,
    '--release-path', releasePath,
    '--lease-directory', run.leaseDirectory,
  ], { stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
  children.add(child);
  return { child, readyPath, releasePath, resultPath, output: collect(child) };
}

async function runWpfWriter(run: MatrixRun, mode: 'create' | 'replace', sharedDataRoot: string, name: string) {
  if (!aibosWpfDll) throw new Error('AIBOS_WPF_DLL is required.');
  const resultPath = path.join(run.root, `${name}-writer.json`);
  const child = spawn('dotnet', [
    aibosWpfDll,
    '--shared-root-lease-writer-smoke', resultPath,
    '--mode', mode,
    '--temp-root', run.root,
    '--locator-path', run.locatorPath,
    '--shared-data-root', sharedDataRoot,
    '--lease-directory', run.leaseDirectory,
  ], { stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
  children.add(child);
  const output = collect(child);
  const exitCode = await new Promise<number | null>((resolve) => child.once('exit', resolve));
  children.delete(child);
  if (exitCode !== 0) throw new Error(`WPF writer failed (${exitCode}).\n${output()}`);
  return JSON.parse(await fs.readFile(resultPath, 'utf8')) as Record<string, unknown>;
}

describe('H25 locator lease real-process matrix', () => {
  it('H25 readers coexist, block H25 replace, and admit it only after all readers exit', { timeout: 45_000 }, async () => {
    const run = await makeRun('replace');
    const first = startH25Holder(run, 'h25-first');
    const second = startH25Holder(run, 'h25-second');
    await Promise.all([waitForFile(first), waitForFile(second)]);

    expect(writeSharedRootLocator({
      mode: 'replace', locatorPath: run.locatorPath, sharedDataRoot: run.replacementRoot, leaseDirectory: run.leaseDirectory,
    })).toMatchObject({ ok: true, acquired: false, errorCode: 'locator-lease-busy', locatorChanged: false });
    await release(first);
    expect(writeSharedRootLocator({
      mode: 'replace', locatorPath: run.locatorPath, sharedDataRoot: run.replacementRoot, leaseDirectory: run.leaseDirectory,
    })).toMatchObject({ ok: true, acquired: false, errorCode: 'locator-lease-busy', locatorChanged: false });
    await release(second);
    const canonicalReplacementRoot = await fs.realpath(run.replacementRoot);
    expect(writeSharedRootLocator({
      mode: 'replace', locatorPath: run.locatorPath, sharedDataRoot: run.replacementRoot, leaseDirectory: run.leaseDirectory,
    })).toMatchObject({ ok: true, acquired: true, locatorChanged: true, resolvedRoot: canonicalReplacementRoot });
    expect((await fs.stat(path.join(run.leaseDirectory, 'locator.lock'))).size).toBe(0);
  });

  it('reports the protocol-global production lease path described by the vendored fixture', () => {
    const lease = locatorFixture.locatorLease;
    expect(resolveLocatorLeasePath()).toBe(path.join(
      fsSync.realpathSync.native(os.tmpdir()),
      ...lease.directory.relativeSegments,
      lease.identity.fileName,
    ));
    expect(lease.contents).toBe('empty');
    expect(lease.runtimeDeletion).toBe('never');
  });
});

describe.runIf(Boolean(aibosWpfDll))('H25 / exact Aibos WPF locator lease real-process matrix', () => {
  for (const mode of ['create', 'replace'] as const) {
    it(`H25 reader blocks WPF ${mode} and WPF succeeds after release`, { timeout: 45_000 }, async () => {
      const run = await makeRun(mode);
      const holder = startH25Holder(run, `h25-${mode}`);
      await waitForFile(holder);
      const desiredRoot = mode === 'create' ? run.dataRoot : run.replacementRoot;
      expect(await runWpfWriter(run, mode, desiredRoot, `blocked-${mode}`)).toMatchObject({
        ok: true, acquired: false, errorCode: 'locator-lease-busy', locatorChanged: false,
      });
      await release(holder);
      expect(await runWpfWriter(run, mode, desiredRoot, `released-${mode}`)).toMatchObject({
        ok: true, acquired: true, locatorChanged: true, resolvedRoot: desiredRoot,
      });
    });

    it(`WPF reader blocks H25 ${mode} and H25 succeeds after release`, { timeout: 45_000 }, async () => {
      const run = await makeRun(mode);
      const holder = startWpfHolder(run, `wpf-${mode}`);
      await waitForFile(holder);
      const desiredRoot = mode === 'create' ? run.dataRoot : run.replacementRoot;
      expect(writeSharedRootLocator({
        mode, locatorPath: run.locatorPath, sharedDataRoot: desiredRoot, leaseDirectory: run.leaseDirectory,
      })).toMatchObject({ ok: true, acquired: false, errorCode: 'locator-lease-busy', locatorChanged: false });
      const receipt = await release(holder);
      expect(receipt).toMatchObject({ ok: true, pathCount: 7, released: true });
      expect(writeSharedRootLocator({
        mode, locatorPath: run.locatorPath, sharedDataRoot: desiredRoot, leaseDirectory: run.leaseDirectory,
      })).toMatchObject({ ok: true, acquired: true, locatorChanged: true, resolvedRoot: desiredRoot });
    });
  }

  it('mixed readers coexist and mutation waits until both runtimes release', { timeout: 45_000 }, async () => {
    const run = await makeRun('replace');
    const wpf = startWpfHolder(run, 'wpf-mixed');
    await waitForFile(wpf);
    const h25 = startH25Holder(run, 'h25-mixed');
    await waitForFile(h25);
    await release(h25);
    expect(writeSharedRootLocator({
      mode: 'replace', locatorPath: run.locatorPath, sharedDataRoot: run.replacementRoot, leaseDirectory: run.leaseDirectory,
    })).toMatchObject({ ok: true, acquired: false, errorCode: 'locator-lease-busy', locatorChanged: false });
    await release(wpf);
    expect(writeSharedRootLocator({
      mode: 'replace', locatorPath: run.locatorPath, sharedDataRoot: run.replacementRoot, leaseDirectory: run.leaseDirectory,
    })).toMatchObject({ ok: true, acquired: true, locatorChanged: true });
  });
});
