# Issue #21 Cancel Stale Display Loads

## Change

Display-image blob loading now has a cancellable handle API.

`CachedImage` uses the cancellable path for display images, so when a modal or
preview image unmounts or changes source before the fetch finishes, the stale
load can abort instead of continuing to blob creation.

Existing cache behavior is preserved:

- cached object URLs are still reused,
- shared pending loads stay alive while at least one consumer remains,
- thumbnail direct rendering is unchanged,
- the existing `loadCachedImageUrl` promise API remains available for preloads.

## Mechanical Evidence

Representative rapid navigation sequence:

- user moves across 5 display images before the first 4 finish loading,
- first 4 display loads become stale,
- final display load remains current.

Before this change, stale component cleanup only ignored state updates. The 5
fetch/blob operations could continue.

After this change, the representative test verifies:

- 4 stale display loads abort,
- only 1 object URL is created,
- the cache ends with 1 display object URL and 0 pending loads.

Reduction in that sequence:

- stale object URL creation: 4 to 0, a 100% reduction
- completed display blob creations: 5 to 1, an 80% reduction

## Verification

- `corepack pnpm exec vitest run src/lib/clientImageCache.test.ts --reporter=verbose`
- `corepack pnpm exec tsc --noEmit`
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`

Observed result:

- client image cache tests: 4 tests passed
- typecheck passed
- full project verify: 14 test files passed, 82 tests passed
- production build passed
- build output: `/` first load JS 134 kB, shared JS 102 kB
