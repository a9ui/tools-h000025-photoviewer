# Shared-state v1 implementation evidence

This note retains the external API, ABI, packaging, and interoperability
evidence used for Issue #341. Aibos owns the contract; this repository only
implements the pinned snapshots in `contracts/`.

## Context7 preflight

Context7 was queried on 2026-07-23 before product implementation. The enabled
MCP endpoint was `https://mcp.context7.com/mcp`, using only
`resolve-library-id` and `query-docs`.

Resolved libraries:

- Koffi: `/websites/koffi_dev` (220 snippets, Medium source reputation,
  benchmark score 86.72).
- Next.js: `/vercel/next.js` (6,139 snippets, High source reputation,
  benchmark score 82.56).

The retained Context7 queries covered three distinct topics:

1. Koffi Win32 calling convention, opaque `HANDLE`, UTF-16 arguments, pointer
   values, and `CloseHandle`.
2. Koffi native-binary packaging and supported Windows distributions.
3. Next.js native server dependency externalization.

Context7 returned the following implementation-relevant facts:

- Load Win32 libraries with `koffi.load(...)`; declare Win32 functions with
  `__stdcall` and wide-string parameters with `str16`.
- Model a Win32 handle as
  `koffi.pointer('HANDLE', koffi.opaque())`, and pass that type to
  `CloseHandle`.
- Koffi 3 uses platform packages installed through `optionalDependencies`;
  fully supported targets have prebuilt binaries and do not require a local
  C++ compiler.
- Add native packages used by Route Handlers to Next.js
  `serverExternalPackages` so Node loads them with native `require` instead of
  bundling them.

These results were checked against the official [Koffi documentation],
[Koffi packaging migration], and [Next.js `serverExternalPackages`
documentation].

## Win32 lease ABI

The adapter calls `CreateFileW` and `CloseHandle` from `kernel32.dll`.
Microsoft documents that:

- `CreateFileW` takes an UTF-16 path, desired access, share mode, creation
  disposition, flags, and optional template handle.
- conflicting access/share combinations fail with a sharing violation;
  sharing restrictions remain in effect until the handle is closed.
- `OPEN_ALWAYS` opens or creates the file without truncating it.
- failure is `INVALID_HANDLE_VALUE`, not `null` or zero.
- `CloseHandle` ends the sharing restriction and releases the resource.

Contract mapping:

| Lease | Desired access | Share mode | Creation | Lifetime |
| --- | --- | --- | --- | --- |
| reader | `GENERIC_READ` | `FILE_SHARE_READ` | `OPEN_ALWAYS` | process |
| writer | `GENERIC_READ | GENERIC_WRITE` | `0` | `OPEN_ALWAYS` | mutation |

The adapter must compare `koffi.address(handle)` with the pointer-width
all-bits-one `INVALID_HANDLE_VALUE`, keep the opaque handle private, and call
`CloseHandle` exactly once. It must never write to or delete `locator.lock`.

## Dependency and distribution evidence

The adopted dependency is exactly `koffi@3.1.2`:

- npm license: MIT.
- npm author and sole listed maintainer: Niels Martignene / `koromix`.
- package integrity:
  `sha512-wVwuE21TBl8/si6E0hPorKR2PJ2q33mEWVETANrtSp3kFM8fi2FcD/J5wmxu0T4TBcqmMQ4xKuF1X1ayFmphzw==`.
- unpacked package size: 1,785,267 bytes.
- exact optional Windows packages exist for `win32-ia32`, `win32-x64`, and
  `win32-arm64`; the probed x64 package is MIT and has integrity
  `sha512-FeFC59UU1XX4J3ZaqKrsrEzczzB5qksMJo7/R45vIg8mGNVSLMVE85JRiZpjcp9i5Lbav5Vw47QvwFzBgIfvlw==`.
- the official repository is owned by `Koromix`; GitHub reports an MIT license.
- `pnpm audit --prod` on the isolated probe installation reported no known
  vulnerabilities.

Version updates require explicit changelog, integrity, platform-package,
interoperability, and security review. No automatic range update is allowed.

## Real-process capability probe

All paths and files were synthetic children of OS TEMP. The WPF participant
was built from Aibos commit
`0f22dc98c29e4b354d188d18d5ac7df0d10f00c0`, tree
`c4b3f8379130a1752487c55cef62d0de8167cfaf`, and invoked only through its
published smoke CLI.

Node `fs` is insufficient: while a Node reader handle was open, a second Node
writer acquired the file. There is no Node `fs` fallback.

The Koffi `CreateFileW` probe passed:

- H25 reader blocks H25 writer.
- H25 readers coexist.
- H25 reader blocks WPF writer without locator mutation.
- WPF reader blocks H25 writer.
- H25 and WPF readers coexist.
- writer acquisition succeeds after the final reader releases.
- `locator.lock` remains zero bytes.

The retained Vitest real-process gate then exercised the production H25
adapter, not a probe-only reimplementation. With the exact Aibos WPF DLL
above it passed 7/7 process cases:

- two simultaneous H25 reader processes coexist; H25 replace remains blocked
  after only one exits and succeeds after the final reader exits;
- an H25 reader blocks Aibos WPF `create` and `replace`, leaving the locator
  unchanged, and both operations succeed after release;
- an Aibos WPF reader blocks H25 `create` and `replace`, with the WPF receipt
  reporting all 7 store paths, and both operations succeed after release;
- H25 and Aibos WPF readers coexist; a writer remains blocked until both have
  released;
- H25 resolves the absolute production path from OS TEMP using the exact
  directory and filename in `PV-ROOT-001`.

The durable-state gate passed all 6 IDs / 33 cases, including strict UTF-8,
optional single UTF-8 BOM, UTF-16/32 and invalid UTF-8 protection, the
1,048,576-byte input/output boundary, unknown-field retention, future-schema
protection, delete-confirm fail-safe, and Recent shared/local authority. The
original locator gate passed all 22 discovery/fail-closed cases. Every mutable
fixture and process path was a generated child of OS TEMP.

## 2026-07-23 path-identity hardening

The locator snapshot was advanced from canonical Aibos remote commit
`5335c3457e7d4dac0d487e650b2d18c882933b44`, tree
`8e2d3a135535e9cee529c498117e2ea576c1c93f`. Both repository copies have
SHA-256
`e2988a15b282f4da9c9cd2fed8925be542d227e14a16ade29025a536a2bf18d1`.

The hardening fixes four bounded defects without moving or rewriting user
state:

- existing shared roots resolve to the canonical final filesystem target;
- a redirected TEMP lease descendant and a mismatched opened lock identity
  fail closed;
- locator and shared JSON readers stop after the configured byte boundary
  instead of allocating the whole file first;
- create-only locator publication cannot replace a locator that appears
  concurrently.

The canonical Aibos verifier passed 23/23 reader cases, 6/6 activation cases,
redirected-lease rejection, and the two-process missing/create/replace proof.
The exact Aibos DLL and H25 production adapters then passed the 7/7
real-process reader/writer interoperability matrix. The H25 focused gate
passed 107/107 tests plus TypeScript typecheck. ESLint reported zero errors
and retained one unrelated pre-existing `ImageGrid.tsx` exhaustive-deps
warning. The complete unit suite then passed 736 tests with 14 intentional
skips across 5 files, and the optimized Next.js build passed with only the
existing Turbopack NFT trace warning for the local open route.

## Implementation constraints

- Windows server code only; unsupported platforms fail closed.
- The production path is exactly
  `%TEMP%\aibos-shared-root-locator-leases-v1\locator.lock`.
- Test seams must still resolve an absolute lease directory inside synthetic
  OS TEMP.
- No user-derived lease filename, Node `fs` lock fallback, handle duplication,
  automatic reopen, lock-file contents, or runtime deletion.
- `koffi` is an exact dependency and a Next.js server external.
- Acquisition errors retain the Win32 error code but do not expose raw handles.

[Koffi documentation]: https://koffi.dev/start
[Koffi packaging migration]: https://koffi.dev/migration
[Next.js `serverExternalPackages` documentation]: https://nextjs.org/docs/app/api-reference/config/next-config-js/serverExternalPackages
[Microsoft `CreateFileW` documentation]: https://learn.microsoft.com/windows/win32/api/fileapi/nf-fileapi-createfilew
[Microsoft `CloseHandle` documentation]: https://learn.microsoft.com/windows/win32/api/handleapi/nf-handleapi-closehandle
