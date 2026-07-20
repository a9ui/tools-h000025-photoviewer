# Browser authoritative specification recap

Date: 2026-07-14

## Outcome

The current Browser PhotoViewer is now documented as an implementation-grade,
requirement-ID contract, and the WPF parity plan has been rebuilt from live WPF
source rather than older milestone notes.

Baseline:

- commit: `626b7dd5416f3619ae59fc66d47e79acd1a74fd5`
- production runtime: normal launcher at `http://localhost:3000/`
- tracking: GitHub issue #316
- SQLite: improvement item #33 / job #386

## Delivered

### Browser contract

`docs/browser-feature-contract.md` now contains:

- 143 unique requirement IDs
- runtime and trust boundary
- exact image, metadata, Favorite, Seen, folder-set models
- Landing, scan/SSE, index and cache behavior
- Header, Sidebar, search, filter, sort, folder buckets
- virtualized Grid/List, zoom anchor, selection and drag-out
- right preview, bottom tabs, modal interaction
- Recycle Bin Delete and neighbor continuation
- Enhancement isolation, adapters, MP guards, queue and outputs
- localStorage/shared JSON schemas and concurrency limits
- all 21 local API routes with request/response/status details
- loading/empty/error, responsive, accessibility and performance
- executable acceptance cases, known limitations and source/test map

The API/state contract was re-audited after drafting. Incorrect draft shapes
for ImageFile, SDMetadata, SearchResponse, Favorites PUT, Recent PUT and
Enhancement POST were corrected before publication.

### WPF parity plan

`docs/browser-to-wpf-parity-plan.md` now:

- classifies features as ADOPT / ADAPT / ADD / NATIVE-EXTENSION / DEFER / DROP
- replaces stale claims with live `MainWindow.xaml/.cs` evidence
- makes Quick Search and date presets DROP
- makes Folders collapse, independent Favorite Lv1–5 + All, Unseen dots default
  OFF, zoom anchor, List virtualization, removal of the 1,200-image cap, and
  safe Delete P0
- separates P0A–P0D, P1, P2 and P3
- records large code-behind, shared JSON, migration and Delete risks
- defines when the WPF version may be called Browser-contract complete

## Runtime evidence

Read-only production interaction confirmed:

- current Viewer shell and 8-image existing fixture
- no Quick Search
- no Today / 7d / 30d / This year controls
- Folders section expands and collapses
- independent Favorite level controls and All semantics are present
- Unseen dots is a visibility setting
- single selection, double-click tab/modal, metadata and settings surfaces
- Enhancement queue may be viewed without creating a job
- Browser console final check: 0 errors, 0 warnings

No source Delete or Enhancement action was executed.

## Verification

- `git diff --check`: PASS
- requirement IDs: 143 / 143 unique
- fenced code blocks: balanced
- mojibake scan: PASS
- unit: 22 files / 128 tests PASS
- typecheck: PASS
- production build: PASS
- lint: 0 errors, 2 existing `@next/next/no-img-element` warnings in
  `src/components/CachedImage.tsx`

The first project-verifier attempt was blocked before tests because Corepack
tried to download pnpm 11.7.0 and the npm registry timed out. The installed
local pnpm 11.7.0 and offline package store were then used; unit, lint,
typecheck and build completed as above.

## Open product blocker

The Browser is intended to be local-only, but the live port 3000 listener was:

```text
LocalAddress ::  LocalPort 3000
```

The launcher starts `next start -p <port>` without a loopback host, while the
API has no authentication, Origin validation or CSRF protection and handles
absolute paths. The contract therefore records loopback-only binding as an
unresolved P0 safety acceptance, not as a completed guarantee.

This documentation milestone did not change the launcher because its scope was
documentation-only.

## Constraints preserved

- no user state/cache deletion or reset
- no deployment
- no Browser implementation change
- no Delete or Enhancement implementation change
- no `local-native/**` implementation change
- no external AI consultation
- unrelated root checkout change was not touched

## Recommended next milestones

1. Browser runtime safety: loopback-only bind plus explicit build provenance.
2. WPF P0A: remove obsolete UI and restore Favorite/Unseen/Folders semantics.
3. WPF P0B: full catalog, List virtualization and zoom viewport anchor.
