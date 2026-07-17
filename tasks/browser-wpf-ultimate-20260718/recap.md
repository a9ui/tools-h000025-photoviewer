# Browser / WPF Ultimate Review - Recap

## Outcome

Browser版を製品挙動の正本として再定義し、その契約をWPFへP0〜P2まで適用した。実装、正規仕様、focused verifier、20,000画像stress、同一process reload soak、Browser production runtime、Browser/WPF共有state競合を同じbranchで検証した。

このmilestoneで検証した実装headは`af2bc71624deb7e51e6cb3208351b692646bcd85`、branchは`codex/wpf-ultimate-p0-20260718`。開始時の`origin/main` / user rootは`626b7dd5416f3619ae59fc66d47e79acd1a74fd5`で、branchはcloseout前時点で137 commits aheadだった。

Viewer coreは現行Browser契約のP0〜P2を満たす。P3のWPF Enhancement write ownership、cache quota/eviction、installer/signing/public packagingはこの判定へ含めない。公開、deployment、user port 3000のrestart、user state/cache削除は実施していない。

## Authoritative specification

別AIまたは別実装者は次の順で読む。

1. `docs/ai-implementation-brief.md`
2. `docs/browser-feature-contract.md`
3. `docs/wpf-product-spec.md`
4. `docs/browser-to-wpf-parity-plan.md`
5. `docs/product-review-20260718.md`

正本上の重要な意味:

- Favorite Lv1〜5は独立したexact-match filter。未選択がAllでthresholdではない。
- Unseen dotsは既定OFFの表示設定。toggleはSeen dataを変更しない。
- Quick SearchとToday / 7d / 30d / This year presetはない。Dateはmanual From/Toだけ。
- Foldersは既定展開、折り畳みと復元が可能。
- Grid zoomは画像cardだけを40〜600で変更し、sidebar/header/fontを変えない。見えていた画像anchorを維持する。
- source Deleteは明示操作、直前guard、Windows Recycle Binのみ。Delete後は変更前filtered orderのnext、なければprevious、最後ならempty/modal close。
- passive browse、preview、modal、hoverはEnhancementをenqueueせずworkerを起動しない。
- 1,200件等のsilent capは禁止。virtualization/batchingで全件を扱う。

## Browser updates

- 仕様書をlive UI/API/state/error/acceptanceまで拡張し、WPFが古いmockupやGitHub snapshotへ逆戻りしないroutingを固定した。
- Favorite/Seen hydrationを待つtestへ直し、非同期UI検証を`act` / `waitFor`へ統一した。全suiteのstderr warningを0にした。
- production launcherのprocess ownershipをexact Node entrypointとdirect child treeへ限定した。port番号やrepo root文字列だけで他processを停止しない。
- launcher watchdogは後から同じportを取得した無関係processを停止しない。
- isolated runtimeは`127.0.0.1`へbindし、source revision、dirty flag、build id、server PIDを`/api/runtime`とverifierで証明する。
- 既存のzoom anchor/sidebar固定、Favorite exact levels、Unseen display-only、Folders collapse、Quick Search/date presets不在、Delete adjacent selectionを回帰guardで保持した。

## WPF updates

- Browserの全主要workflowをnative WPFへ揃え、P0〜P2 ledgerをDONEへ整理した。
- 5,000/20,000件catalogで全件を保持し、Grid/List virtualization、rapid search、cancellation、stale generation破棄を固定した。
- partial multi-root scan、unavailable root retry、mixed decode warning、scan cancel、reload ownershipを回復可能にした。
- Favorite/Seen/recent/stateのbounded lock、latest-disk merge、atomic replace、unknown field保持、malformed/future schema拒否、crash residue回復をBrowserと共有した。
- single/bulk Recycle、filter中のneighbor選択、refresh/delete race、external delete/rename、same-path replacementをdead UIなしで再同期した。
- corrupt/locked/replaced image decode時に古いbitmapを残さず、復旧可能なplaceholder/statusへ変えた。
- 極端な縦長画像をno-upscale、pixel budget、long-edge budgetで制限した。UI threadへ巨大bitmapを作らない。
- Modalをnamed focusable dialog surfaceへし、initial focus、Tab/Control+Tab cycle、focused child上のEscape、close後focus return、Automation Name/HelpTextを固定した。
- WPF launcherをmtime判定からprovenance判定へ変更した。repo root、project/target path、git HEAD、source fingerprint、exe SHA256の一致を要求し、missing/unproven/tampered/wrong-root/wrong-revisionはbuildし直す。
- WPF launcherはBrowser、Node、localhost、port 3000、既存WPF processを所有・停止しない。

## Final verification

### Browser

- `pnpm test -- --run`: 53 files PASS / 2 skipped、420 tests PASS / 2 skipped、warning/stderr diagnostics 0。
- `pnpm typecheck`: PASS、diagnostics 0。
- `pnpm lint`: PASS、warning/error 0。
- `pnpm build`: PASS、Next.js 16.2.10 production routes生成。
- isolated production port 3012: `127.0.0.1`だけでlisten、revision `af2bc716...`、`sourceDirty=false`、build id `ftP2x7LKyFkBbYT1s-LfQ`。
- in-app Browser 1280x820: Landing complete、horizontal overflowなし、Quick Search/date presets不在、console warning/error event 0。
- isolated launcher PID/treeだけを停止し、終了後port 3012 listener 0。

### WPF

- `dotnet build -c Release --no-restore`: 0 warnings / 0 errors。
- `verify-wpf-product.ps1 -IncludeReloadSoak`: **42 / 42 PASS**、235,488 ms、`SkipStress=false`。
- 20,000 stress: catalog 20,000 / filtered 20,000 / silent truncate 0、Grid 96 / maximum 384、List 19、load 9,960 ms、tail search 358 ms、heartbeat 16、source 20,000、Enhancement reads/candidates 0。
- oversized decode: 256x16,384 sourceをGrid 35x2,240、Preview 112x7,168、Modal 175x11,200へ制限。peak working-set growth 12.2 MiB、latest selection勝利。
- reload soak: 1,000画像 x 2 folders、24 cycles、explicit cancel 8、supersede 16、stale completion 0、preview/modal 24/24、CTS 73/73、managed growth 11,117,856 bytes、final working-set growth 78,204,928 bytes、stores/source byte-identical。
- Delete raceの初回false negativeは、正しいModal root focusに対して旧testがClose button固定を要求したoracle不整合。正本へ合わせ、focused 3/3、関連4 gates、最終aggregateでPASS。
- cross-runtime Favorite/Seen writer 20 iterations: 40 favorite + 40 seen entries、valid JSON、residue 0、HTTP/port不使用。
- cross-runtime recent writer 20 iterations: Browser/WPF/third writerのhistory merge、unknown field保持、residue 0、HTTP/port不使用。

## Visual evidence

BrowserとWPFは同じ1280x820、同じfixture/stateでLanding、Viewer、Settings、Folders collapsed、Unseen ONを並べて確認した。機能QAはscreenshotだけに依存せず、同じstateの実操作/verifierも通した。

- final Browser runtime: `C:\Users\a9ui\.codex\visualizations\2026\07\13\019f5bd3-0f54-7f60-bf8d-ad521fa114c2\browser-final-runtime-af2bc71-1280x820.png`
- Browser/WPF comparison: `C:\Users\a9ui\.codex\visualizations\2026\07\13\019f5bd3-0f54-7f60-bf8d-ad521fa114c2\comparison-browser-wpf-final-viewer-1280x820.png`
- settings comparison: `C:\Users\a9ui\.codex\visualizations\2026\07\13\019f5bd3-0f54-7f60-bf8d-ad521fa114c2\comparison-browser-wpf-final-settings-1280x820.png`
- collapsed comparison: `C:\Users\a9ui\.codex\visualizations\2026\07\13\019f5bd3-0f54-7f60-bf8d-ad521fa114c2\comparison-browser-wpf-final-folders-collapsed-1280x820.png`

## User data and runtime safety

開始前後で次がbyte-identicalだった。

- WPF state: 1,105 bytes / `16B615768581761B83765D5CE91CF1DB25DE002DE58E6C376F9BD9332C83E644`
- `.cache/favorites.json`: 2,784,842 bytes / `F5664674B38AA47F552821FC554733067AE97CF9C678E877D9D4C4468ED320A5`
- `.cache/seen.json`: 345 bytes / `75C6AD299537FA9D54B9CCAA60573BC746FD321151502B4CEEAA3FC8C940DA55`
- `.cache/recent-folders.json`: 1,282 bytes / `89C03AED080BDB7B8E06A25F4C1D0078B6EBA7FABD3643179E8E515A890586A7`

user root checkoutは`main` / `626b7dd...`、既存の`M next-env.d.ts`だけを保持した。user port 3000は開始前後ともPID 25620、`[::]:3000`、root checkoutの`next start -p 3000`。この既存runtimeは古いroot buildなので、本milestoneでは停止/rebuild/adoptしていない。

## Publication and current limitations

- GitHub Actionsはgateにしていない。
- deployment/publicationは実施していない。
- environment policyでpushできないため、成果はlocal integrated branchにある。GitHubのimplementation truthへはまだ反映されていない。
- SQLite item #34は、isolated launcherのloopback実装/証拠はgreenだが、実port 3000 adoptionが未承認なので`queued`のままにする。
- installer、code signing、auto-update、public distributionは未実装。これはviewer coreのP0〜P2完成判定とは別。

## Next bounded milestones

1. user rootへintegrated branchを安全採用し、明示承認の上でport 3000をrestartしてloopback/current revisionを確認する。
2. userが選んだdisposable real-library copyで、さらに長いWPF reload/performance soakと実操作playtestを行う。
3. 必要ならP3としてWPF Enhancement write ownership、cache quota/eviction、packagingを別milestoneで設計する。
