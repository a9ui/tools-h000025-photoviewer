# Browser folder-open critical-path report (#330)

## Decision

- **FACT:** the scan publication barrier dominates time to the first usable
  result on a cold/changed catalog. The Browser does not issue its first search
  or visible thumbnail request until every root finishes scanning.
- **FACT:** in the baseline cold scan, the changed-file walk plus synchronous
  PNG metadata extraction used a median 2,271.8 ms of the 2,625.4 ms scan
  (86.5%). Search and first-thumbnail work were tens of milliseconds.
- **ADOPT:** read metadata through a bounded async pool (default 8, clamped
  1-16), with one reusable 4 KiB prefix buffer per worker and positioned-read
  fallback for unusual PNG chunk layouts. Cache schema, result order, scan
  publication order, and shared state are unchanged.
- **REJECT:** a synchronous one-read prefix experiment improved cold scan by
  only about 3.7%, below the 10% adoption gate, so it was not retained.
- **DEFER:** warm unchanged scans are led by folder-signature traversal. That is
  a separate measured optimization target; this slice does not alter it.

## Environment

- Source commit: `0d8ce6d23e79cd5827d1d5282ef76953f5a870b6`
- Runtime: Next.js 16.2.10 production build, Node v24.15.0, pnpm 11.7.0
- OS: Windows 11 Home 64-bit, 10.0.26200
- CPU/RAM: Intel Core i7-14700F, 28 logical processors, 34,099,961,856 bytes RAM
- Primary storage reported by Windows: WD_BLACK Gen4 NVMe
- Power plan: Balanced
- Browser/process: fresh Chromium context and fresh `next start` process per
  condition on an isolated `127.0.0.1` port

## TEMP fixture and isolation

- Five independent TEMP pairs; no user image, cache, state, or history path was
  used.
- Each pair: 2 roots, 40 root-level scan targets, 2,000 nested directories,
  and 10,000 deterministic valid synthetic PNGs with unique small Stable
  Diffusion `parameters` metadata.
- Scan cache, thumbnail cache, Favorites, Seen, Albums, Settings, Recent
  folders, Search History, and Enhancement paths were redirected inside the
  generated TEMP run root.
- Cold: fresh process/browser and empty per-variant scan/thumbnail cache.
- Warm unchanged: fresh process/browser, preserved scan cache, fresh thumbnail
  cache, and unchanged fixture mtimes.
- All measured runs returned exactly 10,000 scan and search results.
- The fixture intentionally represents the header/metadata workload under
  change. It is not evidence for large-image thumbnail throughput, which was
  not the optimized stage.

## Baseline phase evidence

Five cold and five warm observations were recorded. P95 is not claimed.

| Condition | Scan raw ms | Scan median | Search median | First thumbnail median | Folder-to-thumbnail median |
| --- | --- | ---: | ---: | ---: | ---: |
| Original cold | 2557.6, 4688.4, 4148.4, 2625.4, 2510.5 | 2625.4 | 69.8 | 33.8 | 2703.0 |
| Original warm | 349.4, 381.0, 350.8, 336.5, 295.3 | 349.4 | 57.3 | 27.6 | 431.8 |

Cold median phase totals across both roots:

| Phase | Median ms | Share of scan |
| --- | ---: | ---: |
| Changed-file walk + metadata | 2271.8 | 86.5% |
| Folder-signature traversal | 168.1 | 6.4% |
| Image glob | 161.8 | 6.2% |
| Index JSON serialize/write | 15.1 | 0.6% |
| Cache entries to `ImageFile` | 14.3 | 0.5% |

Warm metadata work was effectively zero. Warm signature traversal was 210.1
ms, confirming it as the next independent target rather than crediting this
metadata patch for warm-cache noise.

## Candidate evidence

The final 4 KiB/default-8 candidate recorded these five cold scans on the same
fixture set with new empty cache identities:

- 797.5, 756.9, 956.6, 1056.7, 895.5 ms
- Median scan: 895.5 ms
- Median folder-to-first-thumbnail: 969.4 ms
- Directional change from the original median: scan -65.9%; first thumbnail
  -64.1%
- Result/search count: 10,000 in every run

Because the original series had high machine variance, an additional paired,
order-rotated control compared concurrency 1 and 8 on each of the same five
fixture pairs. Candidate scan CV was 5.6%, and it won every pair:

| Pair | Control 1 ms | Candidate 8 ms | Improvement | CPU-time delta |
| ---: | ---: | ---: | ---: | ---: |
| 0 | 3108.8 | 1036.4 | 66.7% | -12.6% |
| 1 | 3523.9 | 1071.9 | 69.6% | -23.7% |
| 2 | 6112.9 | 995.7 | 83.7% | -56.2% |
| 3 | 2435.7 | 1120.2 | 54.0% | +4.5% |
| 4 | 2781.3 | 952.2 | 65.8% | -22.9% |

No paired CPU-time regression exceeded the 10% gate. The tuning control used
the same async code path with concurrency 1, so the comparison isolates bounded
overlap rather than parser behavior.

## Correctness and rollback

- Metadata results are stored by input index after the worker pool completes,
  preserving deterministic cache insertion/result association.
- Aborted scans still publish neither a partial disk cache nor a partial
  in-memory snapshot.
- A 70 KiB ancillary-chunk fixture proves the positioned-read fallback; common
  prefix, non-PNG, and no-parameters cases are also covered.
- `PV_METADATA_READ_CONCURRENCY=1` is the immediate runtime rollback. Full
  rollback is limited to `src/lib/indexer.ts` and `src/lib/pngParser.ts`.
- The temporary phase markers, TEMP cache overrides, and trace runner were
  removed from the product diff after evidence capture.

## Verification record

- Draft PR: `#331` (`codex/browser-load-critical-path-330` -> `main`), reported
  mergeable by GitHub.
- GitHub Actions run `29918286464` did not start any step. Its check annotation
  says account payment/spending-limit state prevented the job from starting;
  this is infrastructure state, not a test failure from the patch.
- `corepack pnpm exec vitest run src/lib/pngParser.test.ts src/lib/indexer.test.ts`
  - passed: 2 files / 17 tests
- `corepack pnpm exec vitest run` with the four explicitly named
  cross-runtime/WPF companion files excluded
  - passed: 71 files / 657 Browser tests
- `corepack pnpm lint`
  - passed with the pre-existing `ImageGrid.tsx:315` hook-dependency warning;
    no errors
- `corepack pnpm build`
  - passed production build
  - existing unrelated NFT warning remains in `src/app/api/open/route.ts`
- Standalone `corepack pnpm exec tsc --noEmit` is not used as positive evidence:
  current main already reports seven tuple/mock typing errors in
  `src/components/ImageGrid.test.tsx`; `next build` type checking passes.
- Workspace service preflight path documented by the copied router was absent:
  `C:\Users\a9ui\Desktop\Tools\System\scripts\check-services.ps1`.

## Scope proof

- Browser source and Browser-only TEMP verification only.
- No WPF/native inspection or test was used for the decision or patch.
- No shared-state schema/root/lock protocol, enhancement behavior, deployment,
  visibility, credential, permission, or repository history change.
