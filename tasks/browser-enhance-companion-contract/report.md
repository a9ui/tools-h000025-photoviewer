# Browser enhancement companion report

## Authority and baseline

- GitHub Issue: [#332](https://github.com/a9ui/tools-h000025-photoviewer/issues/332)
- Browser base: `0d8ce6d23e79cd5827d1d5282ef76953f5a870b6`
- Branch: `codex/browser-enhance-companion-contract`
- WPF/runtime code was not inspected or changed.
- PR #331 remains separate. Its `verify` job started zero steps and is blocked
  by the GitHub account payment/spending-limit harness condition.

## Read-only audit

- No open or closed GitHub Issue directly duplicated the unindexed-source,
  source-path identity, and canonical output ownership defect. Broad Issues
  #316 and #320 do not own this focused Browser change.
- `POST /api/enhance/jobs` previously required an active/fallback index hit.
- `GET /api/enhance/jobs?sourceId=` previously used strict string equality.
- Output GET and managed delete previously checked lexical containment only.
- `PVU_ENHANCE_ROOT` already resolves the exact enhancement root containing
  `jobs.json` and `outputs/`; no store schema or path layout change was needed.
- The production launcher already supports a fixed loopback `--port` that
  fails closed when busy, plus bounded first-available selection when no port
  is specified. No runtime discovery change was needed.

## Adopted implementation

- Treat guarded enhancement POST as the explicit one-shot action. When no
  indexed image matches, accept one absolute path, canonicalize it, require an
  existing regular supported image, reject a one-shot source inside the
  managed enhancement root, and leave the Browser index unchanged.
- Preserve indexed-source spelling to avoid changing existing Browser
  workflows. Persist canonical spelling only for a one-shot source.
- Use one platform-aware path key for active-index lookup and job polling.
  Windows comparison is case-insensitive; case-sensitive platforms remain so.
- Resolve both the lexical and real output roots before serving or deleting.
  A junction/symlink that escapes the canonical output root is rejected.
- Delete the canonical file that passed ownership validation, so a later
  junction swap on the persisted lexical path cannot redirect the removal.
- Keep enhancement `jobs.json` version 1 and every existing job field.

## TEMP-only proof

- Focused contract/regression set: 5 files, 39 tests passed.
- Final Browser suite excluding the four named cross-runtime companion files:
  73 files, 663 tests passed, including the dedicated TEMP E2E. It exercises
  unindexed PNG create, real Sharp worker completion, fresh-store poll, output
  GET, and managed delete.
- Source SHA-256 and source-directory contents remain identical across that
  E2E.
- TEMP junction negative coverage proves the outside-root output is neither
  served nor deleted and both source/output hashes remain identical.
- Every configured source, `PVU_ENHANCE_ROOT`, job file, output, and junction
  target is below a unique OS TEMP fixture. Cleanup refuses a non-TEMP target.
- Changed-file ESLint passed.
- Production `next build` passed. It retains the pre-existing NFT warning from
  `src/app/api/open/route.ts`.
- Standalone `tsc --noEmit` stops only on the seven pre-existing
  `src/components/ImageGrid.test.tsx` tuple/mock errors; it reports no changed
  enhancement file.

## Deferred without conflict

The proposed reader-only `shared-root.v1.json` locator is a separate follow-up.
Its v1 `sharedDataRoot` denotes the directory that directly contains shared
stores, so an existing H25 `.cache` root maps enhancement state to
`sharedDataRoot/enhance`. This change does not create the locator, switch a
write target, or alter missing/invalid locator fallback behavior.
