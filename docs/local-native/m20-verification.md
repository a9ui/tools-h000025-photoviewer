# Local Native M20 Verification

Date: 2026-07-08

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/95

## Scope Decision

M20 changes from a narrow `filter count label` milestone to
`Local Native Migration v1 Closeout Gate`.

Decision:
`LOCAL_NATIVE_MIGRATION_V1_GATE_DEFINED_WITH_NO_EXTRA_MICRO_PARITY_BLOCKER`.

Meaning:

- The v1 finish line is local-native migration for practical local use, not
  full browser parity.
- The old count-label-only PR #96 is superseded. Its CI passed, but it is not
  the M20 closeout vehicle.
- M20 may add code only for a true v1 closeout blocker.
- No blocker is currently identified from M1-M19 evidence, so this M20 change
  records the gate, inventory, classification, and verification route.
- Milestone #26 `Local Native Post-v1 Backlog` contains the after-v1
  implementation list (#97-#118). Those issues do not block M20 unless
  explicitly reclassified as v1-required with evidence.

## v1 Closeout Minimum Conditions

The native viewer can be called `local-native migration v1 complete` when all
of these are true:

1. Normal local viewing runs through `local-native/PhotoViewer.Native` and
   `scripts/start-local-native.ps1` without requiring Node, a browser runtime,
   a webview wrapper, or a localhost HTTP server.
2. A user can choose local folder paths or a folder set, scan/import images,
   refresh, and continue from native SQLite state.
3. The core viewing loop works natively: list/grid browsing, direct preview,
   previous/next navigation, search, favorites, folder visibility, sorting,
   selection, right preview, and detail modal.
4. The common migrated state works natively: explicit browser-state export
   import, favorites, albums, settings summary, gallery restore, large-scroll
   restore, seen/unseen state, date presets/manual dates/date headers, and
   enhanced-only read-only filtering from succeeded jobs.
5. Ordinary browsing, search, preview, detail modal use, date filtering, and
   enhanced-only filtering do not enqueue jobs, start workers, or mutate
   enhancement state.
6. The existing browser app remains preserved: no `src/**` changes for this
   M20 gate, browser typecheck/unit/E2E/full verifier pass as baseline
   regression evidence, and no deployment.
7. Remaining rows are classified with owner/evidence requirement so v1 can
   close without pretending full browser parity is done.
8. Closeout surfaces are updated: GitHub issue/PR/milestone, SQLite job,
   Agmsg pointer/trace and advice classification, and next Codex thread
   handoff for post-v1 work.

## M1-M19 User-Facing Inventory

| Milestone | ユーザー向けの完了済み内容 |
| --- | --- |
| M1 | ローカルnative版の最初の形を作った。高速なファイル走査、SQLite保存、仮想リスト、直接プレビュー、既存favoritesの取り込みを確認した。 |
| M2 | ブラウザ版に近い基本操作をnativeに入れた。リスト/グリッド閲覧、前後移動、favorite変更、検索、favorite絞り込み、最近フォルダ復元、ファイル/フォルダを開く、Recycle Bin削除を確認した。 |
| M3 | 大きいローカルフォルダ向けの土台を強化した。差分スキャン、フォルダ監視、FTS5検索とLIKE fallback、前後プレビューのリングバッファ、画像寸法のヘッダ読み取り、キャッシュ優先制御、性能smokeを追加した。 |
| M4 | 既存H000025状態との互換を進めた。albums membership、明示exportされたbrowser `pvu_*` state、thumbnail/display cache互換測定をnative SQLiteに取り込んだ。 |
| M5 | 以後の判定に使う決定的fixtureと受け入れ枠を固定した。ブラウザ版はbaseline証拠であり、native受け入れはnative証拠で判定する方針を記録した。 |
| M6 | ブラウザ機能とnative対応のmatrixを作り、全行を分類した。空欄やBLOCKEDなしで、残りはDEFERREDとして所有者と証拠条件を明示した。 |
| M7 | native UI smokeを追加した。フォルダscan、直接preview、list/grid、前後/keyboard navigation、favorite操作、検索/no-results、missing-folder status、import summary、enhancement非変更を自動確認した。 |
| M8 | フォルダ表示bucket、sort/display controls、preview/details toggle、thumbnail size、keybinding metadataをnativeに追加し、通常閲覧でenhancement stateが変わらないことも確認した。 |
| M9 | nativeのdetail modalとsettings/right-preview sliceを追加した。modal前後移動、zoom/reset/pan/flip、favorite上下、open external、selected count、splitter persistence、read-only settingsを確認した。 |
| M10 | selection/filter/folder操作を広げた。multi-selection、背景clear、search clear、favorite filter counts、unrated/level filters、folder bucket show/hide/clear、nested folder fixtureを確認した。 |
| M11 | 複数rootのfolder setをnativeで扱えるようにした。2 root scan、root横断検索、全root watcher、root削除、recent folder set、manual refresh、multi-root bucketを確認した。 |
| M12 | folder range selectionを現controlでは延期と決め、かわりにgallery state restoreを追加した。last selected image/index保存、filter後の選択復元、EnsureVisibleを確認した。 |
| M13 | 240枚fixtureで仮想galleryの大きめリストを確認した。180番目の選択、last_selected/last_visible保存、filter後復元、EnsureVisible、preview wait安定化を確認した。 |
| M14 | seen/unseen状態をnativeに移した。browser `pvu_seen_images` export import、native SQLite保存、NEW marker、選択previewでseen化、scan/searchから同じ状態を読むことを確認した。 |
| M15 | date preset filterをnativeに入れた。Today/7d/30d/This year/Clear、CreatedAtUtc local date filtering、search/favorite/folder/sortとの合成、`date_filter`保存を確認した。 |
| M16 | Created sortのlist viewにdate headerを追加した。日付ごとの最初の行にheader markerを出し、Today filter後の再groupingとpassive enhancement isolationを確認した。 |
| M17 | M16のdate headerをgrid viewにも広げた。gridで日付ごとの先頭itemにheader markerを出し、Today filter後のgrid groupも確認した。 |
| M18 | 手動date rangeをnativeに入れた。From/To、from-only、to-only、search/favorite合成、`date_from`/`date_to`保存、list/grid date headerとの合成を確認した。 |
| M19 | enhanced-only filterをnativeに入れた。ただし新規job作成ではなく、既存の成功済みenhancement job stateをread-onlyで読み、search/favorites合成と`enhanced_only_filter`保存、job state非変更を確認した。 |

## Remaining Classification

| Row | M20 classification | Reason / owner / evidence requirement |
| --- | --- | --- |
| v1 finish-line definition and inventory | v1 closeout 必須 | M20の目的そのもの。`docs/local-native/m20-verification.md`、`tasks/local-native-m20/task.md`、matrix更新で固定する。Owner: `codex_pm`. |
| Core native launch/build/fixture verification | v1 closeout 必須 | v1 complete claimの最低証拠。`dotnet build`、native headless/UI smokes、browser baseline verifierを通す。Owner: `codex_pm`. |
| Browser app preservation / `src/**` diff empty | v1 closeout 必須 | 既存browser PhotoViewerを壊していないことがv1条件。`git diff --name-only -- src` emptyが必要。Owner: `codex_pm`. |
| Passive enhancement isolation | v1 closeout 必須 | local viewer化で勝手にenhancement workerを動かさないことは必須。既存smokesでjob state unchangedを再確認する。Owner: `codex_pm`. |
| GitHub/SQLite/Agmsg/next thread closeout | v1 closeout 必須 | milestone closeout条件。Issue/PR/milestone、SQLite job、Agmsg trace/advice classification、次スレッドhandoffを記録する。Owner: `codex_pm`. |
| Remaining richer count labels | v1 後の最善化 | M9/M10でselected/favorite count系は既にある。残るsidebar/filter countの細部はlocal v1利用のblockerではない。Owner: `cursor_impl`; evidence: native UI/headless smoke if adopted after v1. |
| Native enhancement queue management | v1 後の最善化 | Issue #97. M19はread-only enhanced filterだけでv1十分。queue管理はpost-v1。 |
| Original/enhanced image toggle | v1 後の最善化 | Issue #98. output管理とtoggleはpost-v1。 |
| Preview tabs and pinned previews | v1 後の最善化 | Issue #99. detail modal/right preview/gallery restoreはあり、tabs/pinはpost-v1。 |
| Restore recently closed preview tabs | v1 後の最善化 | Issue #100. tab復元はpost-v1。 |
| Hover quick preview | v1 後の最善化 | Issue #101. core previewはあり、hover previewはpost-v1。 |
| Folder bucket range selection | v1 後の最善化 | Issue #102. M12でcontrol replacement/custom decisionが必要と判定済み。post-v1で扱う。 |
| Bulk favorite actions | v1 後の最善化 | Issue #103. single/multi-selectionとfavorite filterはあり、bulk favoriteはpost-v1。 |
| Bulk open actions | v1 後の最善化 | Issue #104. open file/folder pathはあり、bulk openはpost-v1。 |
| Bulk recycle/delete actions | v1 後の最善化 | Issue #105. Recycle Bin pathはあり、bulk destructive actionsはpost-v1。 |
| Delete confirmation and do-not-ask settings | v1 後の最善化 | Issue #106. Disposable fixtureで別途証拠化する。 |
| Prompt and negative prompt metadata display | v1 後の最善化 | Issue #107. core metadataはfilename/size/dimensions/favoriteでv1十分。prompt metadataはpost-v1。 |
| Copy PNG info and prompt metadata | v1 後の最善化 | Issue #108. copy actionsはpost-v1。 |
| Prompt tag actions | v1 後の最善化 | Issue #109. tag actionsはpost-v1。 |
| Search chips and tag-style search UI | v1 後の最善化 | Issue #110. indexed text search/no-resultsはあり、chips/tag UIはpost-v1。 |
| Compact and poster display modes | v1 後の最善化 | Issue #111. list/gridとsort/display controlsでv1十分。 |
| Aspect ratio display controls | v1 後の最善化 | Issue #112. aspect controlsはpost-v1。 |
| Gallery wheel and keyboard zoom | v1 後の最善化 | Issue #113. detail modal zoomはあり、gallery zoom controlsはpost-v1。 |
| Editable keybinding recorder | v1 後の最善化 | Issue #114. read-only settings/keybinding metadataでv1十分。 |
| Malformed import recovery UI | v1 後の最善化 | Issue #115. explicit export importはあり、malformed recovery UIはpost-v1。 |
| Native browser API and error parity matrix | v1 後の最善化 | Issue #116. nativeはHTTP API互換をv1目標にしない。 |
| Complete pvu state persistence migration | v1 後の最善化 | Issue #117. explicit import済みkeyでv1十分。全keyはpost-v1。 |
| Native UI polish and screenshot sweep | v1 後の最善化 | Issue #118. smoke済みUIからHuman Surface polishへ進めるのはpost-v1。 |
| Drag/open parity, placeholder behavior, native thumbnail warmup UI | v1 後の最善化 | Core browse/preview/open paths are already covered. These interaction/perceived-performance details are post-v1 unless separately issued and reclassified. |
| Automatic enhancement workers | 明示的に延期 | guardrail上、普通の閲覧でworker起動は不可。必要なら別milestoneで明示操作だけを設計する。Owner: `human`. |
| Deployment | 明示的に延期 | H project deployは依頼/承認がある時だけ。M20ではしない。Owner: `human`. |
| H000033 route | 明示的に延期 | H000025 local-native laneのcloseoutであり、H000033は触らない。Owner: `human`. |
| Full browser parity as M20 finish line | 明示的に延期 | v1の目的はlocal-native移行完了。完全parityはpost-v1最善化の束として扱う。Owner: `codex_pm`. |

## Post-v1 Backlog Live State

Verified on 2026-07-08:

- Milestone #26 `Local Native Post-v1 Backlog` is open with 22 open issues.
- Issue #95 comment
  https://github.com/a9ui/tools-h000025-photoviewer/issues/95#issuecomment-4910838119
  records #97-#118 as after-v1 by default.
- #97-#118 are all open and assigned to milestone #26:
  #97 native enhancement queue management, #98 original/enhanced toggle,
  #99 preview tabs and pinned previews, #100 restore closed preview tabs,
  #101 hover quick preview, #102 folder bucket range selection,
  #103 bulk favorite actions, #104 bulk open actions,
  #105 bulk recycle/delete actions, #106 delete confirmation settings,
  #107 prompt metadata display, #108 copy PNG info/prompt metadata,
  #109 prompt tag actions, #110 search chips/tag-style search UI,
  #111 compact/poster modes, #112 aspect ratio controls,
  #113 gallery wheel/keyboard zoom, #114 editable keybinding recorder,
  #115 malformed import recovery UI, #116 native browser API/error matrix,
  #117 complete `pvu_*` persistence migration, and #118 UI polish/screenshot
  sweep.

## M20 Blocker Decision

No new implementation blocker is accepted before verification. The already
merged M1-M19 path is enough to define local-native migration v1 if the M20
verification stack passes again and closeout surfaces are updated.

The milestone #26 backlog issues #97-#118, remaining richer count labels, and
drag/open/placeholder/thumb-warmup details are not M20 v1 blockers under the
current evidence. They become v1 blockers only if explicitly reclassified with
new evidence.

## Verification

Commands run on 2026-07-08 in branch
`codex/local-native-m20-v1-closeout-gate`:

- `dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj`
  passed with 0 warnings and 0 errors.
- `powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture`
  passed: images 4, extraImages 2, largeScrollImages 240, createdState none,
  existing favorites/albums/settings/browser export/enhance jobs preserved.
- `-HeadlessImport -BrowserStateExport .\.cache\native\browser-localstorage-export.json`
  passed: favorites 1, albums 2, albumImages 4, browserStateKeys 5,
  seenImages 15, settings 30, images 256.
- `-HeadlessScan -Folder .\.cache\native-fixture` passed: images 4,
  favorites 1, importedFavorites 1.
- `-HeadlessIncrementalScan -Folder .\.cache\native-fixture` passed:
  addedOrUpdated 0, removed 0, unchanged 4.
- `-HeadlessSearch -Folder .\.cache\native-fixture -Search fixture` passed:
  matches 4, indexed true.
- `-HeadlessPerf -Folder .\.cache\native-fixture -Search fixture -PerfIterations 20`
  passed: search p95 0.69 ms, navigation p95 16.27 ms, cacheHitRate 95.0%,
  headerCoverage 100.0%, mutation added/updated/removed 1/1/1, watcherEvents 3.
- `-HeadlessCacheCompat -Folder .\.cache\native-fixture` passed:
  thumb compatible/missing/incompatible 1/2/1, display compatible/missing/
  incompatible 1/3/0.
- `-HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture` passed:
  core UI, folder controls, sorting/display, selection, detail modal,
  settings, enhanced filter, and `enhancementStateUnchanged=true`.
- `-HeadlessFolderSetSmoke -FolderSet .\.cache\native-fixture,.\.cache\native-fixture-extra -Search fixture`
  passed: roots 2, searchMatches 6, recent set/remove/open/manual refresh/
  watcherRoots true, `enhancementStateUnchanged=true`.
- `-HeadlessLargeScrollSmoke -Folder .\.cache\native-fixture-large` passed:
  totalImages 240, targetIndex/restoredIndex 180, virtualMode true,
  restoreSelected/ensureVisible true, `enhancementStateUnchanged=true`.
- `-HeadlessSeenSmoke` passed: importedSeen true, nativeInitiallyUnseen true,
  nativeSeenPersisted true, seenSmokeImages 2, `enhancementStateUnchanged=true`.
- `-HeadlessDateFilterSmoke` passed: preset/manual/search/favorite date
  filters true, persisted range true, `enhancementStateUnchanged=true`.
- `-HeadlessDateSectionSmoke` passed: list/grid/manual range headers true,
  `enhancementStateUnchanged=true`.
- `-HeadlessEnhancedFilterSmoke -Folder .\.cache\native-fixture` passed:
  enhanced-only/search/favorite/clear/persistence true,
  `enhancementStateUnchanged=true`.
- `corepack pnpm typecheck` passed.
- `corepack pnpm test:unit` passed: 16 files, 94 tests.
- `corepack pnpm test:e2e` passed: 2 Chromium tests.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full`
  passed required files, unit, lint, audit, typecheck, build, and E2E. Lint
  kept 0 errors and 2 existing `<img>` warnings in
  `src/components/CachedImage.tsx`.
- `git diff --name-only -- src` returned no files.
