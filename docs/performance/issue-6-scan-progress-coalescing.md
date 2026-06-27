# Issue #6 Scan/API Progress Coalescing

## Scope

This pass targets the scan preparation path used by `/api/scan`.

Before this change, `scanDirectory` reported preparation progress once for
every scan target. `/api/scan` forwards each progress callback as an SSE event,
so folders with many top-level scan targets could produce a large burst before
file scanning starts.

After this change, preparation progress is coalesced by count and time:

- first and final events are always reported;
- dense updates are batched by `getScanProgressStep`;
- if progress would otherwise stay quiet for 400 ms, it is reported anyway.

## Mechanical Reduction

Fast preparation of 1,000 scan targets:

```text
before: 1,001 preparation progress events
after:     41 preparation progress events
reduction: 95.9%
```

Fast preparation of 15,000 scan targets:

```text
before: 15,001 preparation progress events
after:     151 preparation progress events
reduction: 99.0%
```

The 400 ms silence cap keeps slow preparation from becoming invisible while
still avoiding an SSE/UI-update burst during fast preparation.

## Verification

- `corepack pnpm exec vitest run src/lib/scanProgress.test.ts src/lib/indexer.test.ts --reporter=verbose`
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`

The full project verify passed with 72 unit tests, typecheck, and production
build.
