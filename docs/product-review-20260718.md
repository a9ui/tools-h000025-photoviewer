# PhotoViewer Browser / WPF 全体製品レビュー

監査日: 2026-07-18 JST

対象: local-first Browser版とWPF版。remote公開、deployment、cloud/accountは対象外。

## 1. 結論

Browser版は、現行製品の意味と操作を決める正本として十分な完成度にあり、今回の監査で特に弱かった共有state競合、scan failure、複数window index競合、local file route境界、keyboard/focus、preview tab sessionを補強した。

WPF版は「作りかけのUI shell」段階を脱し、Browser契約のP0〜P2を満たす実用viewerになった。5,000枚常用gateと20,000枚stress、bounded virtualization、非同期search/preview/hover、Favorite/Seen共有、Recycle-only Delete、state migration/error/a11y、bulk Favorite/Recycle、Folder range selection/collapse persistence、Prompt tag→search、Explorer FileDropがfixtureで固定されている。以後は実操作visual、競合、性能stressで再現した欠陥を直す段階である。

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
14. recursive scanがnested junction/symbolic linkを追跡して選択root外をcatalogへ取り込み、循環し得る問題。reparse directory skip、visited set、recoverable notice、outside/cycle junction fixtureで固定した。
15. custom maximizeが常にprimary monitorのwork areaを使い、multi-monitorでwindowが移動する問題。現在monitorのnative work area、DPI変換、safe fallbackへ修正した。さらにmaximize中のmonitor切断/解像度/DPI変更後に旧normal boundsへ戻して画面外になる問題を、現在work areaへのbounded restoreで固定した。
16. mixed folder setでunavailable rootとdecode不能画像が同時に存在すると、後のdecode warningがroot warningを上書きし、保持済みmissing rootのRefresh再試行理由が見えなくなる問題。scan warningを合成して両方を同じrecoverable statusへ残した。427文字absolute path、日本語/emoji/空白/apostrophe/mixed case、read-only/exclusive lock/corrupt/外部消失、search/preview/modal/tab、injected Explorer/FileDrop、protected-root guard、fake Recycleをtemp-onlyで通す`verify-wpf-path-robustness.ps1`で固定した。
17. 有効画像のthumbnail/full bitmapを表示した後、同じpathがtruncate/corruptまたはexclusive lockへ変化してfull decodeが失敗すると、旧bitmapをそのまま成功画像として残す問題。current failureだけがRight Preview/Modalのimmediate sourceをclearしてplaceholder + recoverable statusへ移すよう修正した。同名replace、remove/recreate、metadata Refresh競合はlatest generationだけを表示し、8 mutation cycleのold full bitmap weak-referenceはforced GC後0/16 retained、反復final passのmanaged deltaは1 KiB未満、Favorite/Seen/Recent/Enhancement bytes不変を`verify-wpf-decode-mutation.ps1`で固定した。
18. `DecodePixelWidth`がnarrow/tall sourceを指定幅へupscaleし、256×16,384の48KB PNGをModalで1,400×89,600（125,440,000 pixels、direct probeでworking set約481MiB増）へ展開する問題。header dimensionからno-upscale fit、surface別pixel/long-edge budgetを計算し、同fixtureをGrid 35×2,240、Right Preview 112×7,168、Modal 175×11,200へ制限した。1×30,000のsubpixel-width fallback、色一致、rapid stale selection、dispatcher heartbeat 93、peak growth 15.9MiB、source/Enhancement非変更を`verify-wpf-decode-bounds.ps1`で固定した。

## 3. 現在の製品品質評価

| 軸 | Browser | WPF | 根拠・残差 |
| --- | --- | --- | --- |
| Core browse/search | Green | Green | full catalog、query/filter/sort、virtualization、stale cancellation |
| Favorite/Seen | Green | Green | exact Lv1〜5、All、shared locks/merge、additive Seen、Browser/WPF同時20回・別path各40件保持 |
| Delete safety | Green | Green | lexical/canonical active-root + project/app-root guard、Recycle-only、neighbor、bulk partial result、shared UI reconciliation、Favorite/Seen/Enhancement履歴保持 |
| Large library | Green structure | Green at 20,000 | Browser virtualized/paged。WPF exact 20,000、truncate 0、Grid 96/maximum 384、List 19、load 9,960ms、tail search 358ms、tail modal、heartbeat 16 |
| Decode resource bounds | Green route/cache bounds | Green | WPF no-upscale + surface pixel/long-edge budgets。極端な縦長PNGをGrid/Preview/Modalでbounded decodeし、stale guard、heartbeat、working set、色を専用gate化 |
| Error recovery | Green with surface variance | Green | inline scan/search/WPF status。WPF scan cancelはLanding復帰、draft/focus保持、partial publish 0、即再scan優先を専用gate化。multi-rootのmissing/切断rootはrecoverable skipとし、有効rootのcatalogと明示setのRefresh再試行性を保持。unavailable/access/boundary warningまたはpersistence refusalとdecode failureは同時発生時もRetryを保った合成表示にする。Browser全体notification centerは未統一 |
| Multi-window/process | Green for viewer index | Green for shared files | Browser token sessions 8/30分、shared JSON lock。transaction logはない |
| Keyboard/a11y | Green desktop/mobile primary flows | Green overlays + native surfaces | WPF Modalはdialog名/説明、initial focus、Tab/Control+Tab cycle、focused control上のEscape、close後opener returnを実動作で固定。metadata tab/copy、edge zone、Prompt tag、FileDrop drag-out/in、Settings、Delete/bulk confirmationもAutomation Name/HelpTextとfocused verifierを通過 |
| Session restore | Green main state | Green main state | Browser current query/selection/modalは非保存。WPF modal非保存 |
| Visual consistency | 1280x820 reference captured | 1280x820 + 1024x700 captured | 同一fixture/stateのLanding/Viewer/Settings/Folders collapsed/Unseen dotsを比較。WPF shotのOS work-area clampを除去し、Landing Settings入口とno-selection empty guidanceをBrowser正本へ整合。`verify-wpf-visual-layout.ps1` |
| Enhancement isolation | Green | Green read-only | passive enqueue/worker 0。WPF enqueueは意図的DEFER |
| Packaging/operations | Green local launcher | Green local launcher | Browserは通常launcherのloopback/provenance/owned child-tree停止を固定。WPFはrepo root、project/target path、git revision、source fingerprint、exe SHA256のatomic provenanceが全一致する時だけdirect launchし、未証明/改変/別root/別revisionはfail-closed rebuildする。Browser portや既存processを所有・停止しない。公開/installer/deploymentは今回のscope外 |

## 4. 有償品質としての現在線

ローカル私用製品として「日常の画像閲覧・選別・評価で実データを預けられる」線は、次が同時に通れば満たす。

- Browser unit/typecheck/build/lint、isolated production runtime。
- WPF Release build、`verify-wpf-product.ps1 -IncludeReloadSoak`、cross-runtime Favorite/Seen/Recent。`-IncludeReloadSoak`は既定24-cycle soakをaggregate内で実行し、40-cycle以上は長期memory傾向のextended観測として分ける。verified implementation `af2bc71`では42/42、235,488ms。check数はinventoryから動的に決まり、固定数ではなくlive JSONの0 failureをgateにする。
- user state/cacheを削除せずreload。
- port 3000所有processを変更しない。
- passive workflowでenhancement jobs hash不変。
- disposable copy以外をRecycle testしない。
- Browser/WPF screenshotを同じscreen/stateで比較し、明白なcrop/overflow/focus欠陥なし。

まだ「一般販売の完成」と呼ばない理由は、public distribution、installer/update/signing、support/telemetry/privacy noticeが未設計だからである。これはviewer coreの未完成とは分ける。ユーザーが公開しない現在scopeでは、これらを勝手に追加しない。

## 5. 次に価値が高い順

### P2-A WPF native操作完成

Prompt tag→search、Explorerへのnative FileDrop drag-out、Right Preview/Modalのguarded Show in folderは、keyboard/focus/Automation、selection order、path guard、ArgumentList、state/data isolationまでfocused verifier付きで完了した。現行P2 ledger上の実装残はなく、次は統合/visual/stressで再現する欠陥だけを修正する。

追加のoverlay auditで、Modalがopen後も背面galleryへkeyboard focusを残し、Modal内buttonへfocusした後はEscapeがglobal shortcut guardに遮断される欠陥を再現して修正した。Modal rootをnamed focusable cycle surfaceにし、open時initial focus、focused control上のEscape、明示close後のopener/visible-list/Landing fallbackを`verify-wpf-p1b.ps1`で固定した。metadata sidebar/tab/copy controlのAutomation Name/HelpTextも同じgateへ含めた。

### P2-B Delete ownership correctness

Favorite filter中のlevel変更後の即時再同期、sparse paged resultのmodal/Delete full-order neighbor、Source Recycle後のownership方針は完了した。Browser/WPFともsuccessful source pathをcatalog、selection、open/active/pinned/closed tab、preview/modal、persisted UI参照から除去する一方、Favorite/Seenはmulti-owner path履歴、Enhancement job/outputは別削除権限として保持する。WPFはさらにlexical/canonical repository/project/app-root guardとsingle/bulk共通reconciliationを`verify-wpf-delete-correctness.ps1`で固定した。

WPFでRefreshのmetadata処理中にsource Recycleが成功すると、Refresh開始時のold file listが削除pathをcatalogへ復活させるP0 raceを再現して修正した。successful Recycleだけをload generation付きtombstoneへ記録し、stale publishを除外してpost-Delete neighbor/Modal/focusを維持する。failed/cancelは非対象、次のfresh Refreshでは同名再生成sourceを再発見する契約を`verify-wpf-delete-race.ps1`で固定した。

### P2-C 共通性能・回帰

1. rapid search/selection/decode、Grid/List、display/aspect、right-panel resize commit、Favorite/Unseen、tab reorder/close/pinを321件fixtureでstress済み。filter外tab/pinがreloadで消える欠陥をfull-catalog reconciliationへ修正し、最終入力だけの復元、heartbeat、Enhancement 0を`verify-wpf-rapid-ui-state.ps1`で固定した。
2. 300ms state debounce前のwindow closeで最終queryを失う欠陥を、close時exactly-once state-only flushとstale async cancellationで修正した。malformed/future/contended state保護、shared store非変更、atomic residue 0を`verify-wpf-shutdown-state.ps1`で固定した。
3. Browser route worker + WPF writer + 独立writerのRecent同時20回を完了した。さらにWPFのRecent書込みを明示folder-set commitだけへ限定し、通常state/refresh/closeのbyte-identical、latest-under-lock merge、unknown field、12件cap、lock失敗後retryを`verify-wpf-recent-write-ownership.ps1`で固定した。
4. actual WPF processをFavorite/Seen/Recent/ViewerStateのatomic replace直前でabrupt exitさせると、旧実装は30秒超lockを削除したUI操作がzero-timeout判定で再取得せず初回だけ偽busyになり、orphan tempも次回成功後に残り続けた。stale除去成功時だけ同じUI操作でcreate-newを再試行し、Browser/WPF両lock holderがtarget別temp residueを掃除するよう修正した。`verify-wpf-crash-lock-recovery.ps1 -Iterations 3`はactual crash 12回、live owner 4件、malformed/future 8件、2 WPF + Browser Favorite/Seen、2 WPF + Browser/third Recentをtemp-onlyで通し、fresh lock/bytes不変、unknown field保持、lock/tmp residue 0を固定する。
5. 最終aggregateは1,000枚×2 folderを同一WPF processで24 cycle切替/Refreshし、explicit cancel 8、supersede 16、stale completion 0、preview/modal 24/24、load CTS 73/73、managed growth 11,117,856 bytes、final working-set growth 78,204,928 bytes、shared JSON/source byte不変、Enhancement 0でPASSした。先行extended soakは40 cycle、cancel 14、supersede 26、stale 0、preview/modal 40/40、CTS 121/121。working setはwarm 207,273,984、forced-GC後final 569,303,040、max 604,921,856 bytes、managedは13,910,656→25,102,320 bytesだった。後半8 cycleは494,305,280〜595,693,568 bytes、平均559,781,888、OLS slope +10,744,783 bytes/cycleと短区間noiseがある一方、warm-up後32 cycleは16増/15減、OLS slope +288,661 bytes/cycle、cycle 27のpeakを以後13 cycle更新せず、単調増加ではない。forced-GC後のworking-set-minus-managed差は193,363,328→544,200,720 bytesで、native allocator/OS working-set保持として性能観測に残す。`verify-wpf-product.ps1 -IncludeReloadSoak`, `verify-wpf-reload-soak.ps1 -Count 1000 -Cycles 40`。

### P2-D 外部source変化の回復

Refresh前後でTile objectが作り直されるため、存在するclosed preview tabまで全消去される欠陥、外部削除/rename済みpathがpin stateへ残る欠陥、表示中Modalが旧sourceのbitmap/titleを保持する欠陥を再現して修正した。surviving closed tabはpathでrebindし、current root内で消えたopen/active/pin/selection/modal/state参照だけを除去する。Modalはfallback selectionへ同期してfocusを維持し、全source消失時は閉じてOpen folder setへfocusを戻す。Favorite/Seen/Recent/Enhancement JSONはbyte-identical、decode不能sourceはcatalog内placeholderで継続することを`verify-wpf-external-stale-source.ps1`で固定した。

さらに、Refresh前の有効thumbnail/full imageを表示中に同一pathがtruncate/corruptまたはexclusive lockへ変化すると、full decode失敗後も旧bitmapが残る欠陥を再現した。current decode失敗ではRight Preview/Modalのimmediate sourceをclearし、stale/cancel completionでは最新surfaceを変更しない。同名replace、remove/recreate、metadata Refresh競合は色とnative dimensionsでlatest bytesを確認し、修復/lock解除後のRefresh復帰、8 cycle bitmap解放、shared store/Enhancement isolationを`verify-wpf-decode-mutation.ps1`で固定した。

### P2-E Path robustness / mixed failure visibility

Unicode、emoji、空白、apostrophe、mixed-case extension/root、427文字absolute path、read-only、exclusive lock、corrupt、external disappearance、valid + unavailable multi-rootを同一temp fixtureでscanした。catalog/search/preview/modal/tabはpath identityを保ち、lock/corruptはplaceholder継続、unlock後Refreshは復帰、external disappearanceはstale surfaceを除去する。Explorerはinjected `ArgumentList`、drag-outは`DataFormats.FileDrop`、Deleteはprotected-root guard + fake backendだけで検証し、real Explorer/Recycle/source deleteは起動していない。唯一の再現欠陥だったroot warning上書きを合成statusへ修正し、source fingerprint、read-only属性、Favorite/Enhancement bytes、missing root、automation isolationを保持した。

20,000件の最終観測値（このmachine/fixtureの証拠であり製品hard閾値ではない）: fixture 6,865ms、load 9,960ms、final rapid search 358ms、Grid 96/maximum 384、List 19、heartbeat 16、working set 139,272,192→268,611,584 bytes、GC gen0/1/2 = 22/14/3。source 20,000維持、truncate 0、Enhancement 0。初回観測はfixture 5,953ms、load 8,180ms、search 332ms、working set 147,943,424→370,585,600 bytes、GC 25/16/6だった。

### P3 製品判断後

- WPF Enhancement enqueue/queue/cancel/retry/output delete。
- cache quota/eviction。既存user cacheを破壊し得るため、明示migration/retention契約なしに導入しない。
- installer、self-contained publish、code signing、auto-update。
- remote/public/security boundary。

ここだけが現行WPFのCURRENT LIMITATION / DEFER境界であり、P0〜P2 viewer coreの実装残ではない。Browser側のCURRENT LIMITATIONは`browser-feature-contract.md` Section 22に限定して列挙され、WPFへ欠陥として複製しない。Browserの代表残差はloaded subset外のgallery range/keyboard selection、surface別error通知、cache quota不在、local HTTP APIの認証/CSRF不在。WPFの代表残差はEnhancement write ownership、cache quota、配布packageである。

## 6. 変更禁止線

- Quick Search、Today、7d、30d、This yearを復活させない。
- Favorite Lv1〜5をthresholdへ戻さない。
- Unseen dots toggleでSeen dataを変更しない。
- WPFのUnseen dotsはsidebarとApp Settingsの同一persisted settingへ統合済み。両方向同期、既定OFF/migration、reload、keyboard/UIA、Seen JSON byte identityとtemp-only source/cache isolationを専用gateで固定する。
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
- WPF `-IncludeReloadSoak` aggregateのlive check数/全result、cross-runtime 2本、24-cycle final soakと必要時の40-cycle extended soak、5,000/20,000件metrics
- Browser/WPF screenshotsと目視差分
- port 3000 owner before/after
- GitHub issue/PRとSQLite status
- CURRENT LIMITATIONと次milestone候補
