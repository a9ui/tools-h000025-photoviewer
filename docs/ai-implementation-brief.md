# PhotoViewer AI実装ブリーフ

文書状態: **Normative routing / start here for a new implementation agent**

更新日: 2026-07-18 JST

この文書は、PhotoViewerを別AIまたは別実装チームへ渡す時の入口である。ここだけからUIを推測して作らず、以下の正規文書とlive verifierを順に読む。

## 1. 正本の順序

1. [browser-feature-contract.md](./browser-feature-contract.md): 製品の意味、Browserの全画面/API/state/error/acceptance。
2. [wpf-product-spec.md](./wpf-product-spec.md): WPFへの適用、native固有UI/state/safety/acceptance。
3. [browser-to-wpf-parity-plan.md](./browser-to-wpf-parity-plan.md): live完成ledgerと未実装slice。
4. [product-review-20260718.md](./product-review-20260718.md): 現在の品質評価、残差、製品判断。
5. live codeとfocused verifier: 文書との食い違いを発見したら、黙ってどちらかへ合わせず、意図を判定して同じchangeで文書とtestを更新する。

`local-native/ui-mockup/**`のHTML/PNGは初期shellの歴史資料であり正本ではない。Quick Search、Today/7d/30d/This year、Favorite threshold、旧sidebarをそこから復活させてはならない。古いGitHub snapshotやREADMEだけを正本にしてはならない。

## 2. 作るもの

Browser版:

- local-only Next.js viewer。
- 複数folder setのscan/index/search、virtualized Grid/List、right preview、preview tabs、modal、Favorite/Seen、Recycle、明示Enhancement。
- current Browser実装は製品挙動の正本。別実装は契約ID `BR-*` とacceptance `BR-ACC-*` を満たす。

WPF版:

- .NET 8 Windows WPF。Node、localhost、WebView/WebView2を必要としない。
- Browserの意味と結果を維持し、native virtualization、Shell Recycle Bin、Explorer/FileDrop、Windows focus/Automationを使う。
- current WPF実装はP0/P1と主要P2を実装済み。別実装は契約ID `WPF-*` とSection 17 gateを満たす。

## 3. 絶対に変えない意味

- Favoriteはpathごとの0〜5。Lv1〜5 filterは独立exact-match。未選択はAll。thresholdではない。
- Favorite Lv0はunrated。UIのClearは0へ戻す。
- Unseen dotsは表示設定だけで既定OFF。toggleでSeen dataを削除しない。
- Seenは明示閲覧時だけadditiveに増える。hover、scan、prefetchだけでは増やさない。
- Quick SearchとToday/7d/30d/This year presetは存在しない。Dateはmanual From/To。
- Folders sectionは既定展開、折り畳み可能、状態を復元する。
- gallery zoomは画像cardだけを40〜600で変え、sidebar/header/font/List rowを拡縮しない。
- zoom後も現在見ていた画像anchorをviewport内のほぼ同じ位置へ保つ。
- source Deleteは明示操作、guard再検証、Windows Recycle Binのみ。hard delete fallback禁止。
- Delete後は変更前filtered orderのnext、なければprevious。最後の1枚ならempty/modal close。
- passive browse/preview/modal/tab hoverはEnhancement jobをenqueueせずworkerを起動しない。
- 1,200件等のsilent cap禁止。virtualization/batchingで大量catalogを扱う。
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
4. P2操作効率: bulk action、folder range、preview tab reorder、modal polish、native drag-out。
5. P3拡張: Enhancement ownership、packaging等。製品判断前に先回りしない。

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

- isolated production launcherをuser portとは別portで起動し、runtime revision/build provenanceを確認。
- Landing→scan→search/filter→zoom→selection→preview tabs→modal→settings→Recycle確認を実操作。
- reload persistence、2-window index isolation、Favorite/Seen shared merge、expired session Rescanを確認。
- console error/warning 0を目標とし、意図的warningは理由を記録。

WPF:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-ui-regression-guard.ps1
dotnet build .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-p0.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-p1a.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-p1b.ps1
```

加えて`docs/wpf-product-spec.md` Section 17の全focused verifierを実行する。Delete testは専用temp copyだけ、state/favorite/seen/recent testはoverride pathだけを使う。real user state/cache/sourceへtest writeしない。

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
