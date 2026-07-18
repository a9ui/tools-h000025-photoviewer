# PhotoViewer AI実装ブリーフ

文書状態: **Normative routing / start here for a new implementation agent**

更新日: 2026-07-18 JST

この文書は、PhotoViewerを別AIまたは別実装チームへ渡す時の入口である。ここだけからUIを推測して作らず、以下の正規文書とlive verifierを順に読む。

## 1. 正本の順序

1. [browser-feature-contract.md](./browser-feature-contract.md): 製品の意味、Browserの全画面/API/state/error/acceptance。
2. [wpf-product-spec.md](./wpf-product-spec.md): WPFへの適用、native固有UI/state/safety/acceptance。
3. [browser-to-wpf-parity-plan.md](./browser-to-wpf-parity-plan.md): live完成ledger、初回差分の履歴、明示的にDEFERしたslice。
4. [product-review-20260718.md](./product-review-20260718.md): 現在の品質評価、残差、製品判断。
5. [product-quality-review-framework.md](./product-quality-review-framework.md): 8軸の採点、hard no-go、evidence、改善優先順位。
6. live codeとfocused verifier: 文書との食い違いを発見したら、黙ってどちらかへ合わせず、意図を判定して同じchangeで文書とtestを更新する。

`local-native/ui-mockup/**`のHTML/PNGは初期shellの歴史資料であり正本ではない。Quick Search、Today/7d/30d/This year、Favorite threshold、旧sidebarをそこから復活させてはならない。古いGitHub snapshotやREADMEだけを正本にしてはならない。

## 2. 作るもの

Browser版:

- local-only Next.js viewer。
- 複数folder setのscan/index/search、virtualized Grid/List、right preview、preview tabs、modal、Favorite/Seen、Recycle、明示Enhancement。
- current Browser実装は製品挙動の正本。別実装は契約ID `BR-*` とacceptance `BR-ACC-*` を満たす。

WPF版:

- .NET 8 Windows WPF。viewer coreはNode、localhost、WebView/WebView2を必要としない。Modalの明示Enhancement bridgeだけはloopback Browser engineへ委譲する。
- Browserの意味と結果を維持し、native virtualization、Shell Recycle Bin、Explorer/FileDrop、Windows focus/Automationを使う。
- current WPF実装は現行ledger上のP0〜P2と明示Enhancement create/cancel/retry/output-delete bridgeを実装済み。残るP3はWPF単独Enhancement engine/worker ownership、複数output version selector、cache quota/eviction、高度polish、配布packageであり、viewer coreの未実装と混同しない。別実装は契約ID `WPF-*` とSection 17 gateを満たし、P3を製品判断前に混ぜない。

## 3. 絶対に変えない意味

- Favoriteはpathごとの0〜5。Lv1〜5 filterは独立exact-match。未選択はAll。thresholdではない。
- Favorite Lv0はunrated。UIのClearは0へ戻す。
- Unseen dotsは表示設定だけで既定OFF。toggleでSeen dataを削除しない。
- Seenは明示閲覧時だけadditiveに増える。hover、scan、prefetchだけでは増やさない。
- Quick SearchとToday/7d/30d/This year presetは存在しない。Dateはmanual From/To。
- 旧relative DatePresetは具体的なmanual rangeへ一回だけ移行し、runtime/writeは`none|manual`だけ。旧tokenを日々再計算するhidden presetにしない。
- Folders sectionは既定展開、折り畳み可能、状態を復元する。
- gallery zoomは画像cardだけを40〜600で変え、sidebar/header/font/List rowを拡縮しない。
- zoom後も現在見ていた画像anchorをviewport内のほぼ同じ位置へ保つ。
- source Deleteは明示操作、guard再検証、Windows Recycle Binのみ。hard delete fallback禁止。
- Delete後は変更前filtered orderのnext、なければprevious。最後の1枚ならempty/modal close。
- exact Favorite/Unrated filter中のfavorite変更でcurrentが非該当になった時も、同じnext→previous/empty規則で即時再同期する。
- passive browse/preview/modal/tab hoverはEnhancement jobをenqueueせずworkerを起動しない。
- WPFの`AI x2`、Cancel、Retry、managed output deleteだけがloopback Browser APIを呼ぶ。WPFはBrowser server/workerを起動せず、`jobs.json`へ直接writeしない。応答はmodal generationとsource pathを再照合する。
- 1,200件等のsilent cap禁止。virtualization/batchingで大量catalogを扱う。
- WPFの画像decodeはsource headerからno-upscale fitを作り、requested width²×5（絶対10,000,000 pixels）とrequested width×8（絶対long edge 16,384）の両方で制限する。極端な縦長は`DecodePixelHeight`を使い、Grid/Preview/ModalのUI threadを巨大allocationで止めない。
- WPF Modalはnamed focusable dialog surfaceで、open時initial focus、Tab/Control+Tab cycle、focused child上のEscape、close後のopener/visible gallery/Landingへのfocus returnを持つ。metadata tab/copy/edge zoneにもAutomation Name/HelpTextを付ける。
- state/cache/shared JSONをmigrationのために全削除しない。malformed fileを自動上書きしない。
- Browserのport 3000を検証agentが奪わない。isolated runtimeは別portを使う。
- remote公開、deployment、account/cloud syncは明示要求がない限り追加しない。

## 4. 共有データと競合

Browser/WPFが共有する主なfile:

- `.cache/favorites.json`: exact level map。bounded lock、latest-disk merge、atomic replace、malformed拒否。
- `.cache/seen.json`: additive path set。union merge、atomic replace、malformed拒否。
- `.cache/recent-folders.json`: last/recent folder sets。順序、上限、case-insensitive dedupe。

Browser localStorageは即時UI mirrorであり、shared fileより勝手に全置換してはならない。BrowserはFavoriteをbase→incoming差分としてlatest diskへ3-way mergeし、Seenはunionする。WPFも同じ結果を作る。schema、lock、backup、normalizationの詳細は両正規仕様を参照する。

## 5. 実装順

新規または壊れた実装は次の順で直す。

1. source/state安全性: Recycle-only、path/root/index guard、malformed保持、競合merge。
2. P0日常操作: scan、全件catalog、search、Favorite/Seen、zoom anchor、adjacent Delete。
3. P1回復性: async/cancellation、inline errors、state migration、focus/a11y、reload。
4. P2操作効率: bulk action、folder range、preview tab reorder、modal polish、guarded native FileDrop drag-out。
5. P3拡張: WPF単独Enhancement engine/worker ownership、multi-output selector、packaging等。製品判断前に先回りしない。

性能改善のために既存workflowを削らない。UIを軽く見せるために結果をsilent truncateしない。同期I/OやdecodeをUI threadへ戻さない。

## 6. 最低受入証拠

Browser:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-ui-regression-guard.ps1
pnpm test:unit
pnpm typecheck
pnpm build
pnpm lint
```

- test file/test件数は固定の製品要件ではない。0 failureを要求し、実行時のpassed/skipped件数を記録する。意図的skipは対象と理由を明記し、新しいtestが自動的に増えても古い固定件数へ合わせて削らない。
- isolated production launcherはuser portとは別の明示free portで起動する。検証前後にport 3000のownerを記録し、検証agentが停止してよいのは自分が開始したexact launcher PIDとそのdirect child treeだけ。port番号だけを根拠にprocessを停止しない。
- 検証時は`PVU_NO_OPEN=1`、`PVU_COMFY_AUTOSTART=0`で`node .\scripts\prod_launcher.js --port <isolated-port>`を使い、`powershell -File .\scripts\verify-browser-runtime.ps1 -Port <isolated-port> -ExpectedRevision (git rev-parse HEAD)`でruntime revision/build provenanceとloopback bindを確認する。
- Landing→scan→search/filter→zoom→selection→preview tabs→modal→settings→Recycle確認を実操作。
- reload persistence、2-window index isolation、Favorite/Seen shared merge、expired session Rescanを確認。
- console error/warning 0を目標とし、意図的warningは理由を記録。

WPF:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-ui-regression-guard.ps1
dotnet build .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-product.ps1 -IncludeReloadSoak
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-catalog-stress.ps1 -Count 100000 -FolderCount 100 -OverallTimeoutSeconds 180
powershell -ExecutionPolicy Bypass -File .\scripts\verify-cross-runtime-shared-state.ps1 -Iterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\verify-cross-runtime-recent.ps1 -Iterations 20
```

`verify-wpf-product.ps1`は`verify-ui-regression-guard.ps1`と通常の`verify-wpf-*.ps1`、20,000件stressを直列に自動実行し、`-IncludeReloadSoak`で既定24-cycleの同一process reload soakも加える。`verify-cross-runtime-*.ps1`とexact 100,000件/100 folders gateは別実行である。2026-07-18 closeoutのlive inventoryは47 checks / 264,857msで全result green、100,000件gateも100,000/100,000・silent truncate 0・末尾99,999・Grid/List realized 15/9でgreenだった。check数はinventory sanity用のsnapshotであり固定合格件数ではない。40-cycle以上は長期memory傾向のextended観測として必要時に別実行する。closeoutは`-SkipStress`を使わず、live JSONの`checks`と各resultを記録する。失敗時だけ個別のfocused verifierで切り分ける。同じWPF projectを複数processから同時buildすると`obj`が競合するため、統合gate自体は直列実行する。Delete testは専用temp copyだけ、state/favorite/seen/recent testはoverride pathだけを使う。real user state/cache/sourceへtest writeしない。

通常WPF起動はproject rootの`.\start_wpf.bat`を使う。repo root、project/target path、git revision、WPF source fingerprint、exe SHA256をatomic provenanceで照合し、全一致する時だけcurrentとして直接起動する。missing/unproven/invalid/別worktree/別revision/source drift/target改変はfail-closed buildへ戻す。app exit 0はconsoleをpauseせず終了し、非0またはproject missingだけ診断を残す。Browser server、port 3000、既存WPF processを所有・停止しない。`scripts/verify-wpf-launcher-freshness.ps1`で分岐を固定する。

Visual:

- 同一viewport、同一state、同一fixtureの現行Browser referenceとreal WPF `--shot`を並べる。
- crop、overflow、sidebar幅、font、border、focus、empty/error/modal stateを目視し、screenshotだけでfunctional QA完了としない。

## 7. 完成報告の必須項目

- exact commit/branchと変更した契約ID。
- 実装済み / 未実装を分けたfeature ledger。
- test件数、build/lint結果、focused verifier result。
- runtime provenance、user port owner before/after。
- user state/cache/sourceを削除していない証拠。
- screenshot比較と残る視覚差。
- CURRENT LIMITATIONと次のbounded milestone。

「完成」「100%」「有償品質」は、正規acceptanceとlive evidenceが揃った範囲だけに使う。installer、signing、public distributionがscope外ならviewer coreの完成度と分けて記述する。
