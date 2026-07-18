# Browser ↔ WPF main-journey parity audit

Date: 2026-07-18 JST
Audit type: independent source/contract parity audit
Scope: current integrated worktree, user-visible main journeys, persistence and
failure behavior. This is not a screenshot-based visual audit and does not claim
screen-reader or WCAG conformance.

## 1. Verdict

The Browser contract remains the product-meaning source, and the current Browser
implementation covers every user complaint named in this milestone. WPF is no
longer an early shell: its daily viewer journey implements the same audited P0/P1
meanings, most Browser P2 behavior, and useful Windows-native capabilities.

At this audit point:

- **No missing P0 parity contract was found.** Favorite exact levels, display-only
  Unseen dots, retired sidebar controls, Folders collapse, gallery-only zoom,
  zoom anchoring, Recycle-only Delete, and next-then-previous continuation all
  exist in both surfaces.
- **Browser Modal Delete is protected against the reported two-image jump.** The
  component resolves and commits one neighbor once, and a provider integration
  test proves that a delayed search refresh does not advance it a second time.
- **The reproduced WPF large-state P1 is closed.** Favorite and Seen stores at
  100,000 entries now use independent generation-aware writers with
  latest-disk merge, atomic replacement, rollback/Retry and bounded close drain.
  The fault matrix, three repeated latency runs, cross-runtime races, 46-check
  aggregate and 24-cycle reload soak are green.
- The remaining visible differences are mainly **P2 interaction choices**, not
  P0 safety holes: WPF double-click opens the Modal without also creating a
  pinned preview tab, its bottom tab strip is always allocated, and WPF has no
  configurable key-binding editor.
- WPF already exceeds Browser in full-catalog ordering, partial-root recovery,
  Explorer integration, external source mutation recovery, and Windows path /
  Recycle guards.

No known live P0/P1 remains in the audited private-local core. This is not a
claim that every machine, assistive technology, file system or future input is
incapable of exposing another defect.

### 1.1 Eight-axis product score

Scores are evidence-weighted snapshots out of 5, not marketing grades. A score
below 5 identifies remaining proof or polish; it does not silently redefine a
required behavior.

| Axis | Browser | WPF | Evidence-based judgment |
| --- | ---: | ---: | --- |
| Functional completeness | 4.7 | 4.6 | Core browse/search/filter/preview/modal/Delete/state journeys match; WPF deliberately defers Enhancement ownership and some P2 tab/key-binding choices. |
| Correctness and source safety | 4.8 | 4.9 | Recycle-only Delete, ownership revalidation, one-neighbor continuation and failure non-mutation are automated; WPF adds protected-root and native path guards. |
| Persistence and recovery | 4.7 | 4.9 | Both preserve malformed/contended stores; WPF large writers now prove coalescing, retry, close drain and external merge. |
| Performance and scale | 4.4 | 4.8 | Browser remains paged/loaded-subset by contract; WPF proves exact 20,000 catalog and responsive 100,000-entry shared stores. |
| Usability and orientation | 4.7 | 4.5 | Browser zoom/sidebar behavior is mature; WPF preserves zoom and far-tail selection through Grid/List switching but retains a few P2 chrome choices. |
| Visual polish and compact layout | 4.6 | 4.3 | Both exact audit sizes are usable; WPF 1024px view is intentionally denser and still benefits from further non-functional polish. |
| Accessibility evidence | 4.2 | 4.2 | Semantic/keyboard/focus/UIA automation is covered, including Folders ExpandCollapse; real screen-reader, high-contrast and 200% DPI evidence remains manual. |
| Maintainability and verification | 4.7 | 4.9 | Browser has focused unit/E2E/contracts; WPF has focused fault injectors plus a 46-check aggregate and cross-runtime stress. |
| **Private-local audited core** | **4.6 / 92** | **4.6 / 92** | Suitable as the current implementation baseline, with the explicit P2/P3 and manual-evidence limits below. |

## 2. Authority and classification

The Browser specification explicitly defines itself as the reimplementation
source of truth, including behavior, persistence, errors, performance and known
limitations (`docs/browser-feature-contract.md:16-28`). It also says WPF may
replace DOM/CSS/HTTP mechanics but must preserve requirement results, state
meaning, safety boundaries and acceptance conditions
(`docs/browser-feature-contract.md:50-52`).

Classification used below:

- **IMPLEMENTED**: live code and a focused check cover the contract.
- **PARTIAL**: the core outcome exists, but a specified behavior, proof, or
  failure boundary remains.
- **MISSING**: no current implementation of a required meaning was found.
- **NATIVE-EXTENSION**: deliberate Windows-native improvement that does not
  change Browser meaning.
- **DEFERRED**: explicit P3/product-decision scope, not a hidden parity claim.

Line numbers identify this audit snapshot. Prefer the named symbol if later
edits shift a line.

## 3. User-complaint closure matrix

| Complaint / contract | Browser | WPF | Classification and source evidence |
| --- | --- | --- | --- |
| Favorite Lv1–5 independently ON/OFF | Favorites reveals five independent exact-level checkboxes; `All` clears only the level set. | Five checkboxes feed a `HashSet<int>` and exact-OR matching. | **IMPLEMENTED both.** Contract: `docs/browser-feature-contract.md:64-66,602-615`. Browser: `src/components/Sidebar.tsx:313-345`, `src/components/Sidebar.test.tsx:86-99`. WPF: `local-native/PhotoViewer.Wpf/MainWindow.xaml:120-130`, `MainWindow.xaml.cs` symbols `FavoriteLevelFilter_Changed`, `SyncFavoriteFilterControls`, `MatchesFavoriteFilter`; normative detail `docs/wpf-product-spec.md:260-267`. |
| Favorite `All` semantics | No selected level while Favorites is ON means all levels 1–5; a visible `All` button provides a one-action reset. | Empty level set while Favorites is ON means all levels 1–5; UI says `All ratings`, but users clear checked levels individually. | **IMPLEMENTED semantics; P2 ergonomic difference.** Browser: `Sidebar.tsx:319-327`. WPF: `MainWindow.xaml.cs` `SyncFavoriteFilterControls` and `MatchesFavoriteFilter`; verifier scenario is recorded at `local-native/PhotoViewer.Wpf/App.xaml.cs:2487-2529`. A dedicated WPF `All` button would improve discoverability but is not required for correctness. |
| Unseen dots OFF by default, blue marker only, no Seen mutation | `showUnseenMarkers` defaults false; Grid and List derive marker visibility from setting + Seen map. Toggle lives in Settings. | Sidebar and App Settings share one `ShowUnseenDots` state. Grid/List tiles derive the dot from `tile.Unseen`; toggling refreshes presentation only. | **IMPLEMENTED both.** Contract: `docs/browser-feature-contract.md:625-657`. Browser: `src/store/ImageContext.tsx:63-81,2362-2381`, `src/components/ImageGrid.tsx:1118-1133,1229-1245`, `SettingsModal.tsx:214-224`. WPF: `MainWindow.xaml:132-133,719-721`, `MainWindow.xaml.cs` symbols `SetShowUnseenDots`, `RefreshUnseenDots`; spec `docs/wpf-product-spec.md:328-334,499-501`. |
| Folders can collapse and stay collapsed | Button exposes `aria-expanded`, moves focus out of a child before collapse, persists `pvu_view.foldersExpanded`. | Button collapses the content panel; `FoldersSectionExpanded` is saved in state v2 and missing/v1 state migrates to expanded. | **IMPLEMENTED both.** Contract: `docs/browser-feature-contract.md:455-461`. Browser: `Sidebar.tsx:232-236,389-407`, `Sidebar.test.tsx:156-190`. WPF: `MainWindow.xaml:153-166`, `MainWindow.xaml.cs` symbols `ToggleFoldersSection_Click`, `SyncFoldersSectionControls`; spec `docs/wpf-product-spec.md:221-244,503-522`. Accessibility refinement remains below. |
| Quick Search is absent | No live control; test asserts absence. | No live control; historical mockups are explicitly non-authoritative. | **IMPLEMENTED both.** Contract: `docs/browser-feature-contract.md:72-84`. Tests/guard: `Sidebar.test.tsx:101-110`; `scripts/verify-ui-regression-guard.ps1`. WPF source rule: `docs/wpf-product-spec.md:14,236-244`. |
| Today / 7d / 30d / This year are absent | Only manual From/To and conditional Clear exist. | Only manual DatePicker From/To exists; legacy tokens are migration reader input only. | **IMPLEMENTED both.** Contract: `docs/browser-feature-contract.md:72-84,499-510,659-665`. Browser: `Sidebar.tsx:359-384`. WPF: `MainWindow.xaml:134-149`; migration contract `docs/wpf-product-spec.md:269-276`. |
| Zoom does not resize sidebar/text | Browser changes `view.thumbSize` only; sidebar is fixed at 240px with hidden horizontal overflow and a shrinkable slider. | WPF changes card width only. Sidebar/right widths, header/font and List row size are separate. | **IMPLEMENTED both.** Contract: `docs/browser-feature-contract.md:740-759`. Browser: `ImageGrid.tsx:255-291`, `src/app/globals.css:602-618,902-933`, `Sidebar.test.tsx:125-136`. WPF: `MainWindow.xaml:262-269`; spec `docs/wpf-product-spec.md:336-343`. |
| Zoom keeps the viewed area / avoids getting lost | Browser captures the wheel-target image or a viewport anchor and restores its viewport offset after reflow. | WPF captures a realized center-row image/path and restores the vertical offset after layout; repeated zoom smoke records bounded drift. | **IMPLEMENTED both.** Browser: `ImageGrid.tsx:262-289,703-770`, `ImageGrid.test.tsx:351-414`. WPF: `MainWindow.xaml.cs` symbols `CaptureGridZoomAnchor`, `RestoreGridZoomAnchorAfterLayout`; verifier code `App.xaml.cs:2906-2932`; spec `docs/wpf-product-spec.md:336-343`. A 2D center-distance choice would be a P2 WPF refinement. |
| Modal Delete moves exactly one image, not two | One request owns the navigation lock, resolves the delete neighbor once, and commits one selection. Late search compaction does not navigate again. | Success snapshots the pre-delete filtered order, reconciles the source, selects next or previous once, then reopens the existing Modal if it was visible. | **IMPLEMENTED both.** Contract: `docs/browser-feature-contract.md:1023-1053`. Browser: `src/components/ImageModal.tsx:557-594`, `ImageModal.test.tsx:145-174`, `ImageModal.delete.integration.test.tsx:63-152`. WPF: `MainWindow.xaml.cs` symbols `ExecuteDelete`, `ReconcileSuccessfulSourceRecycle`; spec `docs/wpf-product-spec.md:444-474`; smoke scenarios `App.xaml.cs:2789-2844`. |

## 4. Main-journey parity audit

### 4.0 Exact Browser / WPF journey map

This table is the shortest implementation handoff for another engineer or AI:
each row names the user intent, the required state transition, and the current
surface-specific implementation. A row marked `IMPLEMENTED` means the user
outcome matches; it does not require Browser DOM mechanics to be copied into
WPF.

| # | User journey and invariant | Browser implementation | WPF implementation | Current parity / primary evidence |
| --- | --- | --- | --- | --- |
| 1 | **Choose an ordered folder set.** Add/remove/paste folders, reopen Last/Recent, and do not discard the input after an error. | Landing owns an ordered folder set and shared Recent sets; scan failures return inline with Retry/Dismiss while retaining input. | Landing owns ordered roots, Last/Recent and browse/paste; Explorer folder drop is additionally accepted. Unavailable roots stay visible for Retry. | **IMPLEMENTED + WPF native extension.** Browser contract `docs/browser-feature-contract.md:298-346,416-427`; WPF spec `docs/wpf-product-spec.md:113-145`. |
| 2 | **Scan without stale publication.** Progress may update only the owning run; Cancel or a newer intent prevents an older completion/error from publishing. A successful open enters Viewer without modifying Favorite/Seen. | SSE generation rejects late progress/complete/error; Cancel returns immediately to usable Landing and preserves folder/state/cache. | One load generation owns catalog publication, recent completion and status; cancellation/supersession, including an all-unavailable newer intent, defeats the older run. Partial available roots may publish with skipped-root status. | **IMPLEMENTED.** Browser `docs/browser-feature-contract.md:379-416`; WPF `docs/wpf-product-spec.md:127-145`, `MainWindow.xaml.cs` symbol `LoadFolderSetAsync`; focused WPF scan verifier `scripts/verify-wpf-scan-cancel.ps1`. |
| 3 | **Search/filter the complete current catalog.** Query searches filename + prompt; exact Favorite levels OR together; Unrated excludes Favorite; date is manual From/To; folder hide and sorting compose. | Server search/index token supplies paged order; client filters loaded results where documented. Five exact levels and visible `All` are independent controls. | Full local catalog is queried, filtered and sorted before bounded realization. Five exact-level checkboxes feed a level set; an empty set while Favorites is on means All. | **IMPLEMENTED.** Browser `docs/browser-feature-contract.md:435-689`; `Sidebar.tsx:313-407`. WPF `docs/wpf-product-spec.md:209-294`; `MainWindow.xaml:120-166`; `MainWindow.xaml.cs` symbols `MatchesSearch`, `MatchesFavoriteFilter`, `ApplyFilters`, `ComputeFilterResult`. Dedicated WPF `All` control is P2 ergonomics only. |
| 4 | **Use the intended sidebar.** Folders starts expanded, can collapse and persists; Quick Search and Today/7d/30d/This year remain absent. | Folders button exposes `aria-expanded`, hides its content and persists `pvu_view.foldersExpanded`. Source tests and regression guard reject retired controls. | Folders content collapses and `FoldersSectionExpanded` persists/migrates to expanded. Its custom native peer exposes UIA `ExpandCollapse` and the verifier drives Expanded → Collapsed → Expanded. No live retired controls exist. | **IMPLEMENTED.** Browser `docs/browser-feature-contract.md:455-510`; `Sidebar.test.tsx:101-110,156-190`. WPF `docs/wpf-product-spec.md:221-244,269-276`; `ExpandCollapseButton.cs`; regression guard: 18 files PASS. |
| 5 | **Browse Grid/List without UI scale damage.** Gallery zoom changes image/card size only; sidebar, header, text and List rows do not scale. Plain/Alt wheel remain scroll. | Slider and Ctrl/Cmd+wheel update `thumbSize`; CSS fixes desktop sidebar at 240px and prevents horizontal overflow. | Slider/buttons update Grid card width only; shell/sidebar/right-panel/font/List sizing are independent. | **IMPLEMENTED.** Browser `docs/browser-feature-contract.md:695-759`; `ImageGrid.tsx:255-291`; `globals.css:602-618,902-933`. WPF `docs/wpf-product-spec.md:295-343`; `MainWindow.xaml:262-269`. |
| 6 | **Stay oriented through zoom.** Capture a visible image and its viewport offset before reflow, restore it afterward, and clamp to scroll bounds. | Wheel target anchors image-under-cursor; otherwise a viewport-near image is selected and its offset restored after layout. | Captures a realized center-row path and restores vertical offset after layout; repeated smoke asserts bounded drift. | **IMPLEMENTED.** Browser `ImageGrid.tsx:262-289,703-770`; `ImageGrid.test.tsx:351-414`. WPF `MainWindow.xaml.cs` symbols `CaptureGridZoomAnchor`, `RestoreGridZoomAnchorAfterLayout`; `App.xaml.cs:2906-2932`. Two-dimensional anchor choice is a P2 refinement. |
| 7 | **Select and preview without accidental state changes.** Click/range/toggle selection follows current order; opening/selection marks Seen, but Favorite-button-only, zoom, metadata, prefetch and marker toggles do not. | Grid/List selection drives Right Preview; Unseen dots are derived from setting + Seen map. | Full-catalog selection drives Right Preview; Grid/List tile `Unseen` is presentation-only and refreshes from the same setting/state. | **IMPLEMENTED.** Browser `docs/browser-feature-contract.md:625-657,789-853`; `ImageGrid.tsx:1118-1133,1229-1245`; `ImageContext.tsx:2362-2381`. WPF `docs/wpf-product-spec.md:313-334,365-393`; `MainWindow.xaml:132-133,719-721`; symbols `SetShowUnseenDots`, `RefreshUnseenDots`. |
| 8 | **Manage Preview tabs across query/filter/reload.** Activate, pin, close, reopen, reorder and persist tabs; same-catalog filtering keeps a valid snapshot, while successful different-catalog replacement clears old-catalog references. | Bottom tabs hide when empty. Double-click creates/pins a tab and opens Modal. Local tab/pin state restores only valid current-catalog IDs. | Bottom strip reserves 40px and says `No preview tabs`; tab actions and persisted ordered paths/active/pins are full-catalog based. Double-click currently opens Modal only. | **CORE IMPLEMENTED; two P2 choices.** Browser `docs/browser-feature-contract.md:814-889,1471`; `ImageGrid.tsx:405-412,1149-1153,1266-1270`; `ImageContext.test.tsx:1817-1975`. WPF `docs/wpf-product-spec.md:365-420,503-520`; `MainWindow.xaml:15-19,502-516`. |
| 9 | **Inspect one image in Modal.** Navigation follows the filtered order and wraps; zoom/pan/flip/chrome/metadata/Prompt-to-search work without background gallery shortcuts firing. | Modal owns focus/keyboard/pointer behavior and rejects stale async neighbor resolutions after query/order generation changes. | Native Modal follows the full filtered order, retains zoom between images, and uses dialog-local input/focus behavior. | **IMPLEMENTED.** Browser `docs/browser-feature-contract.md:891-992`; WPF `docs/wpf-product-spec.md:422-442`. |
| 10 | **Delete one source safely and continue exactly once.** Confirm if enabled; recycle only; on success remove source references, retain Favorite/Seen/Enhancement history, then select pre-delete next or previous once. Cancel/failure changes nothing. | Token/path ownership is revalidated at the API boundary. A navigation lock resolves one neighbor once; late search compaction cannot advance again. | Canonical source ownership is revalidated immediately before native Recycle. Success snapshots pre-delete order, reconciles source/catalog/tabs, selects next/previous once and reopens Modal only if it was visible. | **IMPLEMENTED.** Browser `docs/browser-feature-contract.md:994-1066`; `ImageModal.tsx:557-594`; component + provider Delete tests. WPF `docs/wpf-product-spec.md:444-487`; symbols `ExecuteDelete`, `ReconcileSuccessfulSourceRecycle`; `App.xaml.cs:2789-2844`. |
| 11 | **Change Settings honestly.** A failed persistent write must not look saved; Retry keeps user intent; diagnostics/clipboard are read-only. | Shared Settings commits only after HTTP success, `ok: true`, and exact echo; failed drafts/toggles retain or restore their visible value and expose Retry. | Confirm-before-delete, Unseen-dot state and diagnostics persist in versioned ViewerState; unknown fields survive and writes are lock + atomic replace. Fixed shortcuts are documented rather than configurable. | **CORE IMPLEMENTED; ownership/key editor P2.** Browser `docs/browser-feature-contract.md:1234-1317`; `SettingsModal.tsx:109-159,181-224,246-289`. WPF `docs/wpf-product-spec.md:497-575`; `MainWindow.xaml:709-733`. |
| 12 | **Reload/refresh/recover without cross-catalog ghosts.** Same-catalog refresh and failed/cancelled replacement preserve valid tabs/state; successful different-catalog replacement clears volatile catalog-owned references only. Malformed/locked stores keep original bytes and show a recoverable error. | Browser normalizes field-locally, journals Favorite/Seen, merges shared state under lock and atomically replaces. Different-catalog clear is success-gated. | WPF versioned state, recent/favorite/seen stores and source mutation recovery preserve valid full-catalog references. Large Favorite/Seen writers are generation-aware, coalescing, retryable and close-drained. | **IMPLEMENTED.** Browser `docs/browser-feature-contract.md:1319-1482,1697-1724`; `ImageContext.test.tsx:1817-1975`. WPF `docs/wpf-product-spec.md:394-420,503-552,588-600`; `SharedStoreWriter.cs`; writer verifier `scripts/verify-wpf-shared-state-writer.ps1`. |
| 13 | **Never start Enhancement through ordinary viewing.** Browse, preview, Modal navigation, Favorite, Seen and Delete must not enqueue jobs or start workers. | Full Enhancement controls exist only behind explicit actions. | Reads succeeded Enhancement output only; no enqueue/cancel/retry/output-delete ownership. | **Browser IMPLEMENTED; WPF deliberate P3 DEFERRED.** Browser `docs/browser-feature-contract.md:64-71,1060-1066`; WPF `docs/wpf-product-spec.md:489-495`. |

### Step 1 — Landing, folder set and scan

Health: **IMPLEMENTED; WPF has native improvements.**

Browser exposes add/remove/paste, last folder set, recent sets, scan progress,
cancel/failure recovery and a successful transition to Viewer. WPF keeps the
same ordered multi-root meaning and additionally accepts Explorer folder drop,
preserves unavailable roots for Retry, skips reparse escapes, and publishes the
available subset without losing the requested set. See
`docs/wpf-product-spec.md:113-145`.

The current WPF scan generation is the owner: cancel/stale runs cannot publish
catalog/state/Recent. The current milestone also adds the missing supersession
case where an all-unavailable newer intent must defeat an older delayed valid
scan. That behavior is represented in `LoadFolderSetAsync` and
`scripts/verify-wpf-scan-cancel.ps1`.

Native advantage:

- Explorer folder drag-in (`docs/wpf-product-spec.md:357-363`).
- Partial multi-root success with explicit Retry (`docs/wpf-product-spec.md:135-145`).
- No silent 1,200-item product cap; catalog and realization are separate
  (`docs/wpf-product-spec.md:176-191`).

### Step 2 — Viewer shell, search, filters and Folders

Health: **IMPLEMENTED.**

Both surfaces provide a persistent sidebar, query input, exact Favorite /
Unrated meaning, manual Created-date range, folder buckets, sort, view/style /
aspect and gallery size. WPF's query grammar and full-catalog filter/sort are
specified at `docs/wpf-product-spec.md:248-294`.

The Browser's retired controls are guarded in source and tests, and the
cross-surface regression guard passed this audit run for 18 files. WPF includes
an additional `Unseen only` filter. It is independent from blue-dot visibility,
so it is a safe **NATIVE-EXTENSION**, not a reinterpretation of the display
toggle.

Browser communicates folder expanded state through `aria-expanded`. WPF uses
`ExpandCollapseButton` and a native Automation peer; the focused verifier drives
Expanded → Collapsed → Expanded and checks persisted/migrated state. Real
screen-reader narration remains a manual evidence item, not a known P1 source
gap.

### Step 3 — Gallery, selection and zoom

Health: **IMPLEMENTED.**

Browser virtualizes sparse/paged results; WPF owns the full filtered order and
uses bounded Grid materialization plus recycling List containers. This WPF
choice improves the Browser's documented loaded-subset limitation
(`docs/browser-feature-contract.md:771-788`) without changing user-visible
ordering.

Current generated WPF 20,000-image evidence
`%TEMP%/photoviewer-wpf-catalog-selection-roundtrip-final-v2.json` reports:

- 20,000 requested/catalog/filtered, silent truncate 0;
- Grid realized 96, maximum 384; List realized 22;
- far-tail canonical selection, Grid container highlight and Grid → List → Grid
  round-trip remain on `stress-019999.png`;
- mode round-trip 32 ms, Grid visual sync 38 ms;
- internal dispatcher max gap 292 ms;
- external unresponsive streak 254 ms, below the 750 ms gate.

That closes the earlier 3.4–4.7 second tail-selection regression. The full
aggregate still needs to consume this final implementation before closeout.

### Step 4 — Right Preview and Preview tabs

Health: **IMPLEMENTED core; two P2 interaction differences.**

Both surfaces provide single-selection preview, Favorite actions, metadata,
open/delete actions, multi-selection bulk actions, tab activate/pin/close /
reopen/close-all, hover, reorder, middle-close and reload persistence. Browser
contract: `docs/browser-feature-contract.md:814-889`. WPF contract:
`docs/wpf-product-spec.md:365-420`.

WPF persistence is correctly full-catalog based: search/filter changes do not
erase a valid open tab or pin, while a Refresh removes paths that actually left
the catalog. WPF saves ordered `PreviewTabPaths`, `ActivePreviewTabPath` and
`PinnedPreviewPaths` (`docs/wpf-product-spec.md:394-412,503-520`). Browser now
also clears every old-catalog volatile/persisted tab reference only after a
different catalog successfully replaces the old one; failure and same-catalog
refresh preserve them (`src/store/ImageContext.tsx` symbol
`clearCatalogOwnedUiState`; `ImageContext.test.tsx:1817-1975`).

P2 differences to decide explicitly:

1. Browser Grid/List double-click creates and pins a tab before opening Modal
   (`src/components/ImageGrid.tsx:405-412,1149-1153,1266-1270`). WPF's current
   `CardsList.MouseDoubleClick` / `RowsList.MouseDoubleClick` only calls
   `OpenModal`; tab creation is an explicit Right Preview action. Either adopt
   Browser double-click semantics or record the native divergence in the WPF
   spec.
2. Browser hides the bottom bar when there are neither open nor closed tabs
   (`docs/browser-feature-contract.md:856-875`). WPF reserves a 40px bottom row
   and shows `No preview tabs` (`MainWindow.xaml:15-19,502-516`). Auto-collapsing
   the empty bar would return useful gallery space without changing tab state.

### Step 5 — Modal and source Delete

Health: **IMPLEMENTED.**

The Modal journey is broadly aligned: filtered order, wrap navigation,
zoom/pan/flip, chrome toggle, edge zones, swipe, metadata tabs/copy, Prompt tag
to search, focus cycle and return. Browser: `docs/browser-feature-contract.md:891-992`.
WPF: `docs/wpf-product-spec.md:422-442`.

Both Delete paths are Recycle-only and revalidate source ownership immediately
before the destructive boundary. Both retain Favorite/Seen/Enhancement history,
purge only owned volatile/persisted source references after success, leave all
state unchanged after failure/cancel, and choose the neighbor from the
pre-delete filtered order. Browser contract:
`docs/browser-feature-contract.md:996-1066`; WPF contract:
`docs/wpf-product-spec.md:444-487`.

The Browser two-step-jump complaint is specifically closed by both a component
and real-provider integration test. The focused Browser parity suite executed
for this audit passed 113/113 tests across Sidebar, ImageGrid, ImageModal,
Delete integration, Settings, and ImageContext.

### Step 6 — Settings, persistence and failure states

Health: **IMPLEMENTED in both audited surfaces.**

Browser Settings now commits shared key bindings and delete confirmation only
after HTTP success, `ok: true`, and exact value echo. It preserves a rejected
draft or rolls back a failed toggle and provides inline Retry
(`docs/browser-feature-contract.md:1236-1294`;
`src/components/SettingsModal.tsx:109-159,181-224,246-289`). Runtime diagnostics
are read-only and abort stale requests (`docs/browser-feature-contract.md:1305-1317`).

WPF Settings provides confirm-before-delete, the same Unseen-dots state from
two surfaces, safe read-only diagnostics and clipboard failure text
(`MainWindow.xaml:709-733`; `docs/wpf-product-spec.md:497-552`). Viewer state is
versioned, unknown-field preserving, lock-protected and atomically replaced.

The reproduced large shared Favorite/Seen P1 is accepted. `SharedStoreWriter`
routes 1 MiB+ stores through immutable per-key generations, one worker per
store, latest-disk-under-lock merge, atomic replacement, generation-safe
rollback/Retry and close drain. The fault matrix proves coalescing, stale
completion, external writer merge, malformed/future refusal, composite Retry,
reload barriers and pending/failed close behavior.

Three current 100,000-entry repetitions are green: Modal p95
7.159/9.803/12.169 ms, Favorite p95 45.148/41.155/42.445 ms and dispatcher max
gap 50.561/50.412/55.704 ms. Absolute 50/65/110 ms limits remain mandatory.
The relative gate is `large <= max(control × 2.5, control + 10 ms)` so sub-frame
scheduler jitter does not create a false RED; its executable self-test still
rejects a 5→20 ms relative regression and the old 186/251/460 ms P1 baseline.
All repetitions also prove pending-before-close, deferred close, final pending
zero and exactly one final state flush.

Relevant source: `local-native/PhotoViewer.Wpf/SharedStoreWriter.cs` and
`MainWindow.xaml.cs` symbols `ShouldUseFavoriteWriter`,
`ShouldUseSeenWriter`, `WriteFavoriteBatch`, `WriteSeenBatch`,
`ApplyFavoriteWriteResult`, `ApplySeenWriteResult`, and
`DrainThenCloseAsync`. Acceptance design:
`tasks/product-perfection-audit-20260718/wpf-shared-writer-plan.md`.

Settings differences that need an explicit product decision, but are not P0:

- Browser supports configurable key bindings; WPF currently uses the fixed
  native shortcut table (`docs/wpf-product-spec.md:554-575`). This is a **P2
  feature gap**.
- Browser delete confirmation is owned by shared `.cache/settings.json`; WPF
  stores it in local ViewerState. The safety meaning matches, but cross-runtime
  preference synchronization does not. Either document runtime-local ownership
  as intentional, or adopt the shared setting with the same refusal/echo
  guarantees. Do not silently merge owners.

### Step 7 — Empty, error and recovery behavior

Health: **IMPLEMENTED core; real assistive-technology proof incomplete.**

Browser keeps scan input after error, separates zero results from request
failure, preserves the last successful search results, rejects malformed shared
documents without overwriting them, and exposes Retry/Dismiss inline
(`docs/browser-feature-contract.md:1697-1724`).

WPF distinguishes scan failure, partial-root skip, no supported images,
filter-empty and decode failure; malformed/contended persistence retains bytes;
recoverable status includes next action where available
(`docs/wpf-product-spec.md:588-600`). It additionally handles external
delete/rename, in-place corrupt/lock/replace, decode staleness and current-source
recovery (`docs/wpf-product-spec.md:405-420`).

Both implementations have semantic/test-level accessibility coverage, but
neither this source audit nor screenshots can prove screen-reader speech,
high-contrast visibility, 200% text behavior, or full keyboard order on the
user's actual Windows setup. That remains an evidence gap. WPF's Folders
expanded state is the one concrete source-level gap identified above.

### Step 8 — Enhancement isolation

Health: **Browser IMPLEMENTED; WPF intentionally DEFERRED beyond read-only.**

Ordinary browsing must never enqueue Enhancement or start a worker in either
surface. WPF reads succeeded outputs but does not enqueue/cancel/retry/delete
Enhancement work (`docs/wpf-product-spec.md:489-495`). This is explicit P3 scope,
not an undisclosed P0/P1 omission. Browser remains the only full Enhancement
control surface unless a later product decision assigns native ownership.

## 5. Remaining gaps by severity

### P0

**None found in the audited main journeys.** This means “no live source/test gap
found”, not “every machine and input is impossible to break.” The final local
aggregate, isolated runtime and user-state/cache checks are recorded in the
closeout recap.

### P1

No known live P1 remains. The following is **manual evidence still owed**, not a
confirmed implementation defect: run a bounded real-Windows keyboard,
Narrator/NVDA, 200% text and high-contrast pass on Landing → Viewer → Folders →
Settings → Delete confirmation. Treat any failure as a new finding.

Browser large Favorite/Seen full-map persistence, large sparse preview restore
and client-filter page boundaries remain **P2 measure-first candidates**, not
confirmed failures. Do not redesign them from speculation.

### P2

1. Decide WPF double-click tab creation parity.
2. Collapse the empty WPF tab strip or explicitly keep it as native chrome.
3. Decide whether WPF should support configurable keys and whether delete
   confirmation should be runtime-local or shared.
4. Improve WPF zoom anchor selection from vertical-only center distance to the
   closest realized item in both X and Y if visual comparison shows value.
5. Clarify WPF Favorite `All`: a dedicated button would match Browser and make
   the empty-level meaning easier to discover.
6. Add an actor-pending guard to the smoke-only `ImportPvu*` helper before that
   helper is ever promoted into a product entry point. No current UI caller
   reaches it.

### P3 / explicit product decision

- WPF Enhancement enqueue/worker/cancel/retry/output delete.
- cache quota/eviction policy.
- installer, signing, auto-update and public distribution. The user explicitly
  keeps this product private/local, so these should not displace viewer quality.

## 6. Windows-native strengths and next opportunities

Already implemented **NATIVE-EXTENSION** strengths:

- guarded Explorer FileDrop drag-out and folder drag-in;
- Show in folder and shell open with canonical-path validation;
- direct Windows Recycle Bin without Browser command transport;
- full query-order model instead of Browser loaded-page selection limits;
- partial-root scan, reparse-boundary defense and long/Unicode path handling;
- external source delete/rename/corrupt/lock/replace recovery;
- current-monitor maximize/restore and WPF-native virtualization.

Future native opportunities, each requiring a separate bounded design:

1. **External-change inbox, not blind auto-refresh.** Use `FileSystemWatcher` as
   a hint, coalesce bursts, show “N source changes detected”, and let the user
   refresh. Preserve Favorite/Seen/tabs and reuse existing stale-source guards.
2. **Windows accessibility polish.** High-contrast token verification, UIA
   value/range patterns for zoom and splitter, and real screen-reader evidence.
3. **Native tab ergonomics.** Make the Browser double-click/pin behavior a
   deliberate setting or adopt it directly, auto-collapse empty tab chrome,
   and preserve all current reload/filter/source-lifecycle guarantees.

## 7. Recommended next three bounded milestones

1. **WPF compact-layout + accessibility evidence.** Review the current 1024px
   sidebar/gallery density, decide empty-tab and double-click behavior, and run
   the manual Narrator/high-contrast/200% DPI journey without changing safety or
   persistence architecture.
2. **Browser large-state measurement.** Measure full-map Favorite/Seen writes,
   sparse preview restoration and page-boundary filter mutations before choosing
   any architecture change.
3. **Native external-change experience.** Add a non-destructive, coalesced
   watcher notification and explicit Refresh path. Prove rename/delete/
   recreate, large bursts, disconnected roots, source/shared-state isolation
   and zero automatic Recycle/Enhancement action.

## 8. Evidence executed by this audit

- Browser unit suite: **54 files PASS / 2 skipped; 445 tests PASS / 2 skipped**.
- Browser typecheck, lint, production build and Playwright **3/3 PASS**.
- Cross-surface retired-control/sidebar regression guard: **18 files, PASS**.
- WPF aggregate: **46/46 PASS in 241,988 ms**, including exact 20,000 catalog,
  far-tail Grid/List round-trip, 100,000-entry writer gate and 24-cycle reload
  soak.
- Cross-runtime Favorite/Seen and Recent stress: **20 iterations each, PASS**.
- Read and checked current Browser contract, WPF product spec, parity ledger,
  live Browser components/store/tests, live WPF XAML/code-behind, verifier
  dispatch and current 20,000-item result.
- Did not use GitHub Actions, external AI, deployment, real user cache mutation
  or real-source Delete. All destructive/fault checks used bounded TEMP fixtures.

Evidence limit: this document is an implementation/contract audit. Final visual
parity must use same-state screenshots from the current Browser production build
and real WPF `--shot`, and screenshots still do not prove accessibility.
