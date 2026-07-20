# WPF large-catalog and Browser-parity recovery recap

## Status

**GREEN for this milestone.** The exact 100,000-image / 100-folder problem,
full-catalog reachability, visible-first thumbnail loading, Created-only date
sections, Browser-compatible aspect behavior, Modal interaction, shared state,
and explicit Enhancement actions are implemented and verified on branch
`codex/wpf-ultimate-p0-20260718`. Browser application code was not changed.

This means the reported WPF workflows are usable and regression-protected. It
does not mean every future commercial concern is complete; the explicit
remaining boundary is listed below.

## Reproduced causes

- The old Grid exposed a 96-item initial window and 96-item batches capped at
  384. The underlying catalog could contain 100,000 paths, but the UI scroll
  extent did not, so reaching the tail required about 1,041 window advances.
- The old exact 100,000-image/single-folder baseline took 20,480ms: scan
  2,261ms, materialize 1,741ms, metadata 15,356ms. Metadata was on the
  first-use critical path.
- Date grouping was derived outside Created sort, so Modified/Name/Random could
  show inappropriate date sections.
- Thumbnail scheduling followed the capped Grid window and List did not decode
  from its actual visible containers.
- A linked worktree resolved `.cache` under that worktree instead of the common
  checkout, splitting Browser Favorite/Seen/Recent/thumbnail/Enhancement
  history from WPF.
- Gallery aspect and Modal edge/backdrop/control behavior did not fully express
  the Browser contract. WPF could read successful Enhancement output but had no
  explicit action bridge.

## Adopted implementation

- `VirtualizingWrapPanel` owns the full pixel extent for every filtered item,
  realizes only visible rows plus two overscan rows, jumps directly to an
  index/path, preserves the zoom anchor, and draws Created-only date headers.
  There is no `Load more`, batch advance, 384-item cap, or silent truncation.
- Grid and recycling List bind the complete filtered collection. List schedules
  thumbnail decode from visible containers; both modes retain the same
  canonical path and multi-selection across mode changes and far-tail jumps.
- Root enumeration is bounded to four workers. A complete lightweight path
  catalog is published before dimension/PNG prompt metadata; metadata streams
  into the open Viewer with generation/cancellation guards. Scan cancel remains
  available during enumeration and pre-publication preparation, while
  background metadata never sends the user back to Landing.
- Browser thumbnail-cache lookup is read-only and accepts precise
  sub-millisecond mtime keys plus the legacy integer fallback. Linked worktrees
  resolve the common checkout before selecting shared `.cache`.
- Original uses contain with a fixed 2:3 card, 1:1 uses square cover, and 2:3
  uses portrait cover.
- Modal uses 28% previous / 44% image / 28% next hit regions, closes from the
  black backdrop, keeps control clicks isolated, and retains keyboard,
  zoom/pan, metadata, Favorite, external-open, Folder, and Recycle actions.
- Modal `AI x2` is the only WPF create action. It calls the loopback Browser API;
  queued/running supports Cancel, failed/canceled supports Retry, and succeeded
  managed output supports a separate confirmed output delete. WPF never starts
  the Browser server/worker and never writes `jobs.json` directly. Source/path
  and Modal generation guards discard responses that arrive after navigation.
- Metadata arriving after early catalog publication refreshes the current
  Right Preview and Modal only when the selected path/generation still matches.
  Persistence refusal warnings are not overwritten by later decode warnings.

## Final verification

All destructive and persistence checks used generated TEMP fixtures and
override paths.

- Release build: **0 warnings / 0 errors**.
- Product aggregate:
  `verify-wpf-product.ps1 -IncludeReloadSoak` — **47 checks PASS** in
  264,857ms, including the 20,000-item stress gate and 24-cycle same-process
  reload soak. Log:
  `C:\Users\a9ui\AppData\Local\Temp\photoviewer-wpf-product-final-d17d178ec5c84bb4a9a41eb957305bbc.log`.
- Exact large catalog:
  `verify-wpf-catalog-stress.ps1 -Count 100000 -FolderCount 100` — **PASS**.
  Catalog/filtered/Grid source were 100,000/100,000/100,000, silent truncate 0,
  Grid/List realized 15/9, normal and Created last visible index 99,999, List
  tail thumbnail 165ms, Viewer ready 4,975ms, full load 33,792ms, flat zoom
  35/50ms with 0px drift, Created zoom 43/60ms with 0px drift, dispatcher gap
  584ms and external WM_NULL gap 522ms under the 750ms gate, working set
  143,036,416→321,769,472 bytes, source count unchanged, cleanup successful.
  JSON:
  `C:\Users\a9ui\AppData\Local\Temp\photoviewer-wpf-catalog-100k-final-5b49ac63cfc542ca875787967b4a0137.json`.
- Explicit scan cancellation: enumeration cancel 0ms and pre-publication cancel
  1ms; prior catalog, draft, state, Recent, Seen, source, and unrelated cache
  remained intact.
- Delete race: preview/Modal delay, sparse navigation, partial bulk failure,
  Refresh collision, and same-name regeneration passed. Delete selects exactly
  one next neighbor (otherwise previous), with stale decode results discarded.
- Explicit Enhancement actions: create/cancel/retry/output delete, stale
  response navigation guard, caller/app TEMP-store hashes, source isolation,
  environment restoration, and residue cleanup all passed.
- Cross-runtime Favorite/Seen: 20 Browser-route/WPF iterations produced 40
  Favorite and 40 Seen paths with valid JSON and no residue. Cross-runtime
  Recent: 20 iterations retained all owners, unknown fields, and bounded
  history. Both used no HTTP port and no user cache/source.
- Visual evidence: real WPF Landing/Viewer/Settings/Folders collapsed/Unseen
  states at 1280×820 and 1024×700 were inspected under
  `C:\Users\a9ui\AppData\Local\Temp\photoviewer-wpf-visual-layout-38f63241b6d24a8fa430334ce364adc8`.
  Modal metadata evidence:
  `C:\Users\a9ui\AppData\Local\Temp\photoviewer-wpf-modal-final-c0198150c2b34748a0e16b1f7881c094.png`.

## Safety and operations

- No user source image, Favorite, Seen, Recent, viewer state, thumbnail cache,
  or Enhancement job file was deleted or reset.
- No deployment, GitHub publication, GitHub Actions gate, or external AI
  consultation was used.
- SQLite `improvement_items.id = 41` records this local-only milestone as done.
- During an earlier runtime provenance check, `prod_launcher.js status --port
  3000` unexpectedly restarted the user-owned Browser and exited. The mistake
  was disclosed immediately and the Browser was restored with a hidden direct
  `next start` process (PID 2672 and HTTP 200 at restoration time). All WPF
  verification after that used TEMP processes and did not touch port 3000.
- The unrelated root checkout change `next-env.d.ts` was not modified or
  staged.

## Remaining boundary

- Explicit WPF AI actions require the local Browser engine and its active index.
  A standalone native Enhancement engine/worker owner and multi-output version
  selector remain separate product work.
- Installer/self-contained packaging, signing, and auto-update remain separate.
- Screen-reader, high-contrast, and 200% text/DPI continuous human evidence is
  still manual. UI Automation names, focus cycle, ExpandCollapse, and exact
  screenshots are automated, but screenshots alone are not an accessibility
  certification.
- The 100,000-item first catalog publication can still produce a measured
  ~584ms dispatcher gap, and first-time Created List grouping can be polished
  further. Both are within the current 750ms liveness gate and do not truncate
  or hide files.
