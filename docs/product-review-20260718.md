# PhotoViewer Browser / WPF 全体製品レビュー

監査日: 2026-07-18 JST

対象: local-first Browser版とWPF版。remote公開、deployment、cloud/accountは対象外。

## 1. 結論

Browser版は、現行製品の意味と操作を決める正本として十分な完成度にあり、今回の監査で特に弱かった共有state競合、scan failure、複数window index競合、local file route境界、keyboard/focus、preview tab sessionを補強した。

WPF版は「作りかけのUI shell」段階を脱し、Browser契約のP0/P1を満たす実用viewerになった。5,000枚catalog、bounded virtualization、非同期search/preview/hover、Favorite/Seen共有、Recycle-only Delete、state migration/error/a11y、bulk Favorite/Recycle、Folder range selection/collapse persistenceがfixtureで固定されている。残る中心はP2操作効率とnative polishで、基本データ安全性やsilent truncateを先送りする状態ではない。

正規資料:

- Browser詳細仕様: [browser-feature-contract.md](./browser-feature-contract.md)
- WPF詳細仕様: [wpf-product-spec.md](./wpf-product-spec.md)
- WPF差分/完成順: [browser-to-wpf-parity-plan.md](./browser-to-wpf-parity-plan.md)

## 2. 今回解消した主要リスク

### Browser

1. 旧buildを誤って配信するruntime provenanceとlauncher safety。
2. Favorite/Settings/Recent/Seenの同時更新lost update、malformed overwrite。
3. BrowserとWPFでSeenが分断される問題。
4. scan切断後にserver処理とpartial publishが残る問題。
5. malformed/stale SSEやsearch failureがblocking alert/silent failureになる問題。
6. 別Browser windowのscanがprocess-global indexを奪う問題。
7. image/open/deleteがcurrent viewer index外のabsolute pathを扱える境界。
8. Favorite backupが意図的なempty primaryを復活させる問題。
9. Folders collapse、preview tabs、active tab orderがreloadで失われる問題。
10. 最後のtab close後にmouseでRestoreできない問題。
11. preview tabを並べ替えられない問題。
12. dialog/card/search/folder/tab/resizeのkeyboard/focus/ARIA不足。
13. countがDOM/loaded/shown/indexedを混同する問題。
14. key binding conflictを保存できる問題。
15. current search/filter外preview tabがmodalを開けない理由を示さない問題。

### WPF

1. Quick Searchとdate presetという削除済みBrowser仕様の復活。
2. Favorite threshold filterと単一level state。
3. Unseen dot常時表示とList/Grid不一致。
4. 1,200枚silent capとList full container materialization。
5. zoomでanchorを失い、List/sidebar寸法へ影響する問題。
6. source Delete未実装、またはhard-deleteへ落ち得る危険。
7. shared JSON/stateの同期書込み、競合、malformed overwrite。
8. search/preview/tab hover decodeがUI threadを止める問題。
9. selectionがGrid realization更新で消える問題。
10. right panel固定幅、bulk action不足。
11. modal端でnavigationが止まる問題。
12. open preview tab order/activeがreloadで失われる問題。
13. errorがMessageBox/silent catchへ分散し、focus/shortcut/a11y証拠がない問題。

## 3. 現在の製品品質評価

| 軸 | Browser | WPF | 根拠・残差 |
| --- | --- | --- | --- |
| Core browse/search | Green | Green | full catalog、query/filter/sort、virtualization、stale cancellation |
| Favorite/Seen | Green | Green | exact Lv1〜5、All、shared locks/merge、additive Seen |
| Delete safety | Green | Green | canonical/root/index/type guard、Recycle-only、neighbor、bulk partial result |
| Large library | Green structure | Green at 5,000 | Browser virtualized/paged、WPF 5,000 P0/search heartbeat。20,000は観測残差 |
| Error recovery | Green with surface variance | Green | inline scan/search/WPF status。Browser全体notification centerは未統一 |
| Multi-window/process | Green for viewer index | Green for shared files | Browser token sessions 8/30分、shared JSON lock。transaction logはない |
| Keyboard/a11y | Green desktop/mobile primary flows | Green primary workflows | chip reorder、WPF残りmodal/tag/drag surfaceは追加検証が必要 |
| Session restore | Green main state | Green main state | Browser current query/selection/modalは非保存。WPF modal非保存 |
| Visual consistency | Needs final screenshot gate | Needs final screenshot gate | tokensは同期。最終同一state比較を行う |
| Enhancement isolation | Green | Green read-only | passive enqueue/worker 0。WPF enqueueは意図的DEFER |
| Packaging/operations | Green local launcher | Usable local launcher | 公開/installer/deploymentは今回のscope外 |

## 4. 有償品質としての現在線

ローカル私用製品として「日常の画像閲覧・選別・評価で実データを預けられる」線は、次が同時に通れば満たす。

- Browser unit/typecheck/build/lint、isolated production runtime。
- WPF Release build、P0/P1、bulk Favorite/Recycle、search/hover/tab/modal verifier。
- user state/cacheを削除せずreload。
- port 3000所有processを変更しない。
- passive workflowでenhancement jobs hash不変。
- disposable copy以外をRecycle testしない。
- Browser/WPF screenshotを同じscreen/stateで比較し、明白なcrop/overflow/focus欠陥なし。

まだ「一般販売の完成」と呼ばない理由は、public distribution、installer/update/signing、support/telemetry/privacy noticeが未設計だからである。これはviewer coreの未完成とは分ける。ユーザーが公開しない現在scopeでは、これらを勝手に追加しない。

## 5. 次に価値が高い順

### P2-A WPF native操作完成

1. prompt tag→search。
2. Explorerへのnative FileDrop drag-out。

### P2-B Browser deep correctness

1. Favorite filter中のlevel変更後にnonmatching modal/selectionを即時再同期する。
2. sparse paged resultでmodal/Delete neighborのfull-order整合を保証する。
3. Delete成功後のFavorite/Seen/pin/enhancement orphan cleanup方針を確定する。

### P2-C 共通性能・回帰

1. 20,000枚観測fixtureでsearch/scroll/memoryを記録する。ただしsilent product capは置かない。
2. rapid window/tab/filter/resizeでstale decode/state writeをstress。
3. Browser 2window + WPF同時Favorite/Seen/Recent更新をrepeatする。
4. process crash/stale lock recoveryとtemp file残留を確認する。

### P3 製品判断後

- WPF Enhancement enqueue/queue/cancel/retry/output delete。
- cache quota/eviction。既存user cacheを破壊し得るため、明示migration/retention契約なしに導入しない。
- installer、self-contained publish、code signing、auto-update。
- remote/public/security boundary。

## 6. 変更禁止線

- Quick Search、Today、7d、30d、This yearを復活させない。
- Favorite Lv1〜5をthresholdへ戻さない。
- Unseen dots toggleでSeen dataを変更しない。
- gallery zoomでsidebar/text/List rowをscaleしない。
- Delete後に先頭/無関係画像へ飛ばさない。
- hard delete fallbackを追加しない。
- passive browseからEnhancementを開始しない。
- testのためにreal user state/cache/sourceを削除しない。
- Browser正本を古いGitHub snapshotや旧WPF READMEで上書きしない。
- port 3000を検証processが奪わない。

## 7. Closeoutで残す証拠

- exact integrated commit/branch
- Browser full gate counts、build/lint result、isolated runtime provenance
- WPF全verifier resultと5,000件metrics
- Browser/WPF screenshotsと目視差分
- port 3000 owner before/after
- GitHub issue/PRとSQLite status
- CURRENT LIMITATIONと次milestone候補
