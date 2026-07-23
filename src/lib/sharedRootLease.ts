import fs from 'fs';
import os from 'os';
import path from 'path';

import koffi from 'koffi';

export const LOCATOR_LEASE_DIRECTORY_NAME = 'aibos-shared-root-locator-leases-v1';
export const LOCATOR_LEASE_FILE_NAME = 'locator.lock';

const GENERIC_READ = 0x8000_0000;
const GENERIC_WRITE = 0x4000_0000;
const FILE_SHARE_READ = 0x0000_0001;
const OPEN_ALWAYS = 4;
const FILE_ATTRIBUTE_NORMAL = 0x0000_0080;
const FILE_NAME_NORMALIZED = 0;
const ERROR_SHARING_VIOLATION = 32;
const INITIAL_FINAL_PATH_CAPACITY = 512;

type NativeHandle = unknown;

interface Win32LeaseApi {
  createFile: (
    fileName: string,
    desiredAccess: number,
    shareMode: number,
    securityAttributes: null,
    creationDisposition: number,
    flagsAndAttributes: number,
    templateFile: null,
  ) => NativeHandle;
  closeHandle: (handle: NativeHandle) => boolean;
  getFinalPathNameByHandle: (
    handle: NativeHandle,
    filePath: Buffer,
    filePathLength: number,
    flags: number,
  ) => number;
  getFileSizeEx: (handle: NativeHandle, fileSize: Buffer) => boolean;
  getLastError: () => number;
}

let win32Api: Win32LeaseApi | undefined;

export type LocatorLeaseErrorCode =
  | 'locator-lease-busy'
  | 'locator-lease-close-failed'
  | 'locator-lease-contents-invalid'
  | 'locator-lease-path-invalid'
  | 'locator-lease-unsupported'
  | 'locator-lease-unavailable';

export class LocatorLeaseError extends Error {
  constructor(
    public readonly code: LocatorLeaseErrorCode,
    message: string,
    public readonly win32Error?: number,
  ) {
    super(message);
    this.name = 'LocatorLeaseError';
  }
}

export class LocatorLease {
  private closed = false;

  constructor(
    public readonly path: string,
    public readonly mode: 'reader' | 'writer',
    private readonly handle: NativeHandle,
    private readonly closeNativeHandle: (handle: NativeHandle) => boolean,
  ) {}

  close() {
    if (this.closed) return;
    this.closed = true;
    if (!this.closeNativeHandle(this.handle)) {
      throw new LocatorLeaseError(
        'locator-lease-close-failed',
        `Could not release the shared-root ${this.mode} lease.`,
      );
    }
  }
}

function getWin32Api(): Win32LeaseApi {
  if (process.platform !== 'win32') {
    throw new LocatorLeaseError(
      'locator-lease-unsupported',
      'The Aibos shared-root lease requires Windows FileShare semantics.',
    );
  }
  if (win32Api) return win32Api;

  const kernel32 = koffi.load('kernel32.dll');
  const handleType = koffi.pointer('HANDLE', koffi.opaque());
  win32Api = {
    createFile: kernel32.func('__stdcall', 'CreateFileW', handleType, [
      'str16',
      'uint32_t',
      'uint32_t',
      'void *',
      'uint32_t',
      'uint32_t',
      handleType,
    ]) as Win32LeaseApi['createFile'],
    closeHandle: kernel32.func('__stdcall', 'CloseHandle', 'bool', [handleType]) as Win32LeaseApi['closeHandle'],
    getFinalPathNameByHandle: kernel32.func('__stdcall', 'GetFinalPathNameByHandleW', 'uint32_t', [
      handleType,
      'void *',
      'uint32_t',
      'uint32_t',
    ]) as Win32LeaseApi['getFinalPathNameByHandle'],
    getFileSizeEx: kernel32.func('__stdcall', 'GetFileSizeEx', 'bool', [
      handleType,
      'void *',
    ]) as Win32LeaseApi['getFileSizeEx'],
    getLastError: kernel32.func('__stdcall', 'GetLastError', 'uint32_t', []) as Win32LeaseApi['getLastError'],
  };
  return win32Api;
}

function invalidHandleValue() {
  const pointerBits = process.arch === 'ia32' ? 32 : 64;
  return BigInt.asUintN(pointerBits, BigInt(-1));
}

function isWithin(root: string, candidate: string) {
  const relative = path.relative(root, candidate);
  return relative === '' || (!relative.startsWith('..') && !path.isAbsolute(relative));
}

function normalizeWin32FinalPath(candidate: string) {
  const uncPrefix = '\\\\?\\UNC\\';
  const devicePrefix = '\\\\?\\';
  const normalized = candidate.toLowerCase().startsWith(uncPrefix.toLowerCase())
    ? `\\\\${candidate.slice(uncPrefix.length)}`
    : candidate.toLowerCase().startsWith(devicePrefix.toLowerCase())
      ? candidate.slice(devicePrefix.length)
      : candidate;
  return path.resolve(normalized);
}

function readOpenedLeaseHandleSnapshot(api: Win32LeaseApi, handle: NativeHandle) {
  let capacity = INITIAL_FINAL_PATH_CAPACITY;
  let pathBuffer = Buffer.alloc(capacity * 2);
  let pathLength = api.getFinalPathNameByHandle(handle, pathBuffer, capacity, FILE_NAME_NORMALIZED);
  if (pathLength >= capacity) {
    capacity = pathLength + 1;
    pathBuffer = Buffer.alloc(capacity * 2);
    pathLength = api.getFinalPathNameByHandle(handle, pathBuffer, capacity, FILE_NAME_NORMALIZED);
  }
  if (pathLength === 0 || pathLength >= capacity) {
    const win32Error = api.getLastError();
    throw new LocatorLeaseError(
      'locator-lease-unavailable',
      `Could not verify the opened locator lease path (Win32 ${win32Error}).`,
      win32Error,
    );
  }

  const sizeBuffer = Buffer.alloc(8);
  if (!api.getFileSizeEx(handle, sizeBuffer)) {
    const win32Error = api.getLastError();
    throw new LocatorLeaseError(
      'locator-lease-unavailable',
      `Could not verify the opened locator lease size (Win32 ${win32Error}).`,
      win32Error,
    );
  }

  return {
    finalPath: normalizeWin32FinalPath(pathBuffer.toString('utf16le', 0, pathLength * 2)),
    size: sizeBuffer.readBigInt64LE(),
  };
}

export function validateOpenedLocatorLeaseSnapshot(
  expectedPath: string,
  snapshot: { finalPath: string; size: bigint },
) {
  const comparableFinal = process.platform === 'win32'
    ? snapshot.finalPath.toLowerCase()
    : snapshot.finalPath;
  const comparableExpected = process.platform === 'win32'
    ? expectedPath.toLowerCase()
    : expectedPath;
  if (comparableFinal !== comparableExpected || snapshot.size !== BigInt(0)) {
    throw new LocatorLeaseError(
      comparableFinal !== comparableExpected ? 'locator-lease-path-invalid' : 'locator-lease-contents-invalid',
      comparableFinal !== comparableExpected
        ? 'The Aibos shared-root locator lease file identity is invalid.'
        : 'The Aibos shared-root locator lease file must stay empty.',
    );
  }
}

function nearestExistingDirectory(candidate: string) {
  let current = candidate;
  while (true) {
    try {
      if (!fs.statSync(current).isDirectory()) {
        throw new LocatorLeaseError(
          'locator-lease-path-invalid',
          'The locator lease path must not traverse a non-directory.',
        );
      }
      return current;
    } catch (error) {
      if (error instanceof LocatorLeaseError) throw error;
      if ((error as NodeJS.ErrnoException).code !== 'ENOENT') {
        throw new LocatorLeaseError(
          'locator-lease-path-invalid',
          'The locator lease directory is unavailable.',
        );
      }
      const parent = path.dirname(current);
      if (parent === current) {
        throw new LocatorLeaseError(
          'locator-lease-path-invalid',
          'The locator lease directory has no available ancestor.',
        );
      }
      current = parent;
    }
  }
}

function validateLeaseDirectory(leaseDirectory: string) {
  if (!leaseDirectory || !path.isAbsolute(leaseDirectory) || leaseDirectory.includes('\0')) {
    throw new LocatorLeaseError('locator-lease-path-invalid', 'The locator lease directory must be an absolute path.');
  }

  const lexicalTempRoot = path.resolve(os.tmpdir());
  const canonicalTempRoot = fs.realpathSync.native(lexicalTempRoot);
  const comparableLexicalRoot = process.platform === 'win32' ? lexicalTempRoot.toLowerCase() : lexicalTempRoot;
  const comparableCanonicalRoot = process.platform === 'win32' ? canonicalTempRoot.toLowerCase() : canonicalTempRoot;
  const resolvedDirectory = path.resolve(leaseDirectory);
  const comparableResolved = process.platform === 'win32' ? resolvedDirectory.toLowerCase() : resolvedDirectory;
  if (!isWithin(comparableLexicalRoot, comparableResolved) || comparableLexicalRoot === comparableResolved) {
    throw new LocatorLeaseError(
      'locator-lease-path-invalid',
      'The locator lease directory must be a child of the OS temporary directory.',
    );
  }

  const existingAncestor = fs.realpathSync.native(nearestExistingDirectory(resolvedDirectory));
  const comparableAncestor = process.platform === 'win32' ? existingAncestor.toLowerCase() : existingAncestor;
  if (!isWithin(comparableCanonicalRoot, comparableAncestor)) {
    throw new LocatorLeaseError(
      'locator-lease-path-invalid',
      'The locator lease directory must not traverse outside the OS temporary directory.',
    );
  }

  fs.mkdirSync(resolvedDirectory, { recursive: true });
  const canonicalDirectory = fs.realpathSync.native(resolvedDirectory);
  const comparableDirectory = process.platform === 'win32' ? canonicalDirectory.toLowerCase() : canonicalDirectory;
  const relativeDirectory = path.relative(lexicalTempRoot, resolvedDirectory);
  const expectedCanonicalDirectory = path.resolve(canonicalTempRoot, relativeDirectory);
  const comparableExpected = process.platform === 'win32'
    ? expectedCanonicalDirectory.toLowerCase()
    : expectedCanonicalDirectory;
  if (!isWithin(comparableCanonicalRoot, comparableDirectory)
    || comparableCanonicalRoot === comparableDirectory
    || comparableDirectory !== comparableExpected) {
    throw new LocatorLeaseError(
      'locator-lease-path-invalid',
      'The locator lease directory must be a child of the OS temporary directory.',
    );
  }
  return canonicalDirectory;
}

export function resolveLocatorLeasePath(leaseDirectory?: string) {
  const directory = validateLeaseDirectory(
    leaseDirectory ?? path.join(os.tmpdir(), LOCATOR_LEASE_DIRECTORY_NAME),
  );
  return path.join(directory, LOCATOR_LEASE_FILE_NAME);
}

function acquireLocatorLease(mode: 'reader' | 'writer', leaseDirectory?: string) {
  const leasePath = resolveLocatorLeasePath(leaseDirectory);
  const api = getWin32Api();
  const desiredAccess = mode === 'reader' ? GENERIC_READ : (GENERIC_READ + GENERIC_WRITE);
  const shareMode = mode === 'reader' ? FILE_SHARE_READ : 0;
  const handle = api.createFile(
    leasePath,
    desiredAccess,
    shareMode,
    null,
    OPEN_ALWAYS,
    FILE_ATTRIBUTE_NORMAL,
    null,
  );
  const win32Error = api.getLastError();
  if (koffi.address(handle) === invalidHandleValue()) {
    throw new LocatorLeaseError(
      win32Error === ERROR_SHARING_VIOLATION ? 'locator-lease-busy' : 'locator-lease-unavailable',
      win32Error === ERROR_SHARING_VIOLATION
        ? 'The Aibos shared-root locator lease is busy.'
        : `Could not acquire the Aibos shared-root locator lease (Win32 ${win32Error}).`,
      win32Error,
    );
  }

  const lease = new LocatorLease(leasePath, mode, handle, api.closeHandle);
  try {
    validateOpenedLocatorLeaseSnapshot(leasePath, readOpenedLeaseHandleSnapshot(api, handle));
    return lease;
  } catch (error) {
    lease.close();
    throw error;
  }
}

export function acquireLocatorReaderLease(leaseDirectory?: string) {
  return acquireLocatorLease('reader', leaseDirectory);
}

export function acquireLocatorWriterLease(leaseDirectory?: string) {
  return acquireLocatorLease('writer', leaseDirectory);
}
