# Issue #19 Thumbnail Warmup Dedup

## Change

`ImageGrid` now routes nearby, visible, and modal thumbnail warmup requests
through a small client-side batcher before posting to `/api/thumbs/warm`.

The batcher:

- coalesces warmup requests made in the same short window,
- removes duplicate image ids for the same directory and filter context,
- upgrades pending `nearby` work to `visible` when the image enters the viewport,
- skips recently sent duplicates for a short window,
- still allows a `visible` request to resend a recently sent `nearby` warmup,
- does not change rendered image `/api/image` requests.

## Mechanical Evidence

Representative simulated scroll plus modal burst:

- nearby range warmup: 80 ids
- visible-priority warmup: 24 ids overlapping the nearby range
- modal background warmup: 18 ids overlapping both ranges

Before this change, that burst could produce:

- 3 POST calls
- 122 id entries across POST bodies

After batching and deduping:

- 2 POST calls
- 80 id entries across POST bodies

Reduction:

- POST calls: 3 to 2, a 33.3% reduction
- id entries: 122 to 80, a 34.4% reduction

The visible 24 ids remain visible priority. The remaining 56 ids stay nearby
priority.

## Verification

- `corepack pnpm exec vitest run src/lib/thumbnailWarmupBatcher.test.ts src/lib/viewerUi.test.ts --reporter=verbose`
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`

Observed result:

- targeted tests: 2 test files passed, 13 tests passed
- full project verify: 13 test files passed, 78 tests passed
- typecheck passed
- production build passed
- build output: `/` first load JS 134 kB, shared JS 102 kB
- the representative burst case asserts 3 overlapping request sources become 2
  dispatches and 80 unique id entries

The route bundle display increased from the previous 133 kB reading to 134 kB.
This issue accepts that small bundle cost because the runtime warmup burst
reduces duplicate POST calls and id entries during scrolling/modal use.
