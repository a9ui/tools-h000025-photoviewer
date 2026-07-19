# PhotoViewer 現在成果物・実装進捗・今後予定 truth table

最終監査日: 2026-07-19 JST

実装正本commit: `792716f9863dd145e028648405c29c340f7a4336`。このmachineの
`refs/heads/main` は、この実装commitに正本仕様・truth tableを加えたdocs-only descendantを指す。

統合仕様: [`docs/photoviewer-authoritative-spec.md`](./photoviewer-authoritative-spec.md)

## 0. 読み方

この文書は「今あるもの」と「これから入れるもの」を分離するlive ledgerである。

| Status | 意味 |
| --- | --- |
| `implemented` | local mainにsourceがあり、対応test/acceptanceがgreen。runtime/launcherを要するsurfaceは同revisionの採用証拠がある |
| `partial` | 類似/基礎機能はlocal mainにあるが、名前付き要件の全部は満たさない |
| `pending` | 計画、調査、別branch/worktree、未採用commit。local mainへ入っていない |
| `blocked` | 実装に必要な外部条件またはユーザー判断が欠け、pendingより強い停止理由がある |
| `FROZEN` | WinForms専用。通常の新機能、追随、製品gateを止め、重大破損/起動不能だけ保守する |

`implemented`へ予定を含めない。20px〜1列とSidebar anchorはBrowserだけ、Favorite source削除確認とshared Search HistoryはBrowser/WPF、拡大画像session回復とModal filmstripはBrowserだけを、local main採用とgate後に昇格した。WPF 20px〜1列/geometry anchor候補は別branchであり、本表ではpendingのままである。

## 1. Evidence register

| ID | 種別 | 2026-07-19に確認した証拠 |
| --- | --- | --- |
| `GIT-LOCAL-001` | commit | 通常rootの検証済み実装revisionは `792716f9863dd145e028648405c29c340f7a4336`。filmstrip/Search History closeoutとruntime provenanceを含む `14ebe6a16a89123e5dfd6e966324522094c1188c` までlocal mainで確定した。実装は旧default origin/mainではなく、この検証済み系列上で継続する |
| `GIT-REMOTE-001` | provenance | default `origin/main=626b7dd5416f3619ae59fc66d47e79acd1a74fd5` はmerge前のため古いまま。Draft PR [#322](https://github.com/a9ui/tools-h000025-photoviewer/pull/322) のremote branchは、ユーザー実行のcanonical push後に検証済みmilestone commit `14ebe6a16a89123e5dfd6e966324522094c1188c` を保持した。`gh pr view` と `git ls-remote` の両方で同一SHA、`OPEN`、`isDraft=true` を2026-07-19に確認し、SQLite improvement item #45をdoneへ更新した。merge/deploymentは行っていない |
| `ADOPT-001` | adopted change | `53dc865...` はBrowserの20〜600/最大1列、Sidebar geometry anchor、Favorite source Delete guard、画像session自動回復、Enhancement output publish retryと、WPF Favorite source Delete guardを含む |
| `ADOPT-002` | adopted change | `44f9716...`〜`792716f...` はBrowser Modal virtualized filmstrip、keyboard Arrow補強、Browser/WPF shared Search History、WPF async/keyboard/a11y、cross-runtime verifier、aggregate競合防止を含む |
| `BR-SRC-001` | source audit | `docs/browser-feature-contract.md`、`src/**`、`e2e/**`、関連scripts/testsを照合。Modal filmstripとSearch History source mapを統合正本へ反映 |
| `BR-TEST-001` | adopted test | Browser unit 61 files / 521 tests、3 files / 3 tests skip、typecheck、production build、lint 0 errors、isolated production Playwright 7/7 green |
| `BR-RUN-001` | normal launcher adoption | 最終docs-only descendantで引数なし`start_viewer.bat`を実行し、`verify-browser-runtime.ps1 -Port 3000 -ExpectedRevision <current HEAD>`をPASSさせる。`127.0.0.1:3000`だけへbindし、ownerは通常rootのproduction tree。build ID/PIDはvolatileで正本値にしない。user-owned dirtyによりsourceDirty=trueは許容 |
| `BR-LAUNCH-001` | launcher | `start_viewer.bat` → `scripts/prod_launcher.js`。revision/build/port/PID provenance、loopback bind、owned process tree、explicit busy port refusal |
| `WPF-SRC-001` | source audit | `docs/wpf-product-spec.md`、XAML、code-behind、support classes、verifier inventoryをlocal mainで照合 |
| `WPF-TEST-001` | adopted test | 現行`-SkipStress` aggregate 50/50 green。従来のstress/reload-soakとexact 100,000 images / 100 foldersもgreen。Release rebuildは0 warnings / 0 errors |
| `WPF-SH-TEST-001` | adopted test | Search History focusedはasync UI、handler相当keyboard helper + compiled XAML wiring、a11y/refocus/Unicode/live-lock Busy writes 0 green。Browser/WPF 20+20 concurrentは42 entries、lost 0、unknown field保持、malformed/future保護、lock/tmp 0。実OS routed-key eventのmanual証拠ではない |
| `WPF-LAUNCH-001` | normal launcher adoption | 最終docs-only descendantで`start_wpf.bat`を実行し、`check-wpf-launch-target.ps1 -Json`の`current / provenance-match / sourceRevision=<current HEAD>`をPASSさせる。fingerprint/exe SHA-256はvolatileで正本値にしない |
| `WPF-RUN-001` | normal runtime | 通常root `local-native/PhotoViewer.Wpf/bin/Release/net8.0-windows/PhotoViewer.Wpf.exe`をexact 1 processで起動し、title `PhotoViewer`、`Responding=True`を確認する |
| `STATE-001` | preservation | ユーザー証拠で通常rootのuser-owned `next-env.d.ts` hashは採用前後で不変。`.playwright-cli/`も本監査では変更していない |
| `FROZEN-001` | product decision | WinFormsは削除/archiveせずFROZEN。解除はユーザー明示のみ |
| `WIN-SRC-001` | frozen commit/launcher | WinForms source最終変更 `17f89682bfbbad486f4980d5b18bc2140d5a1d3c`、wrapper `f164d90ffdd538a10a9886b3d23be985d554968c`、BAT `36759300d21d424741fe5fda30d3dd8ac3a3d9a1`。今回test/runtime未実行 |

ぼくが最終段で実行したのはread-only Browser runtime verifier、WPF launcher checker、process応答確認である。unit/build/lint/E2E/WPF aggregateの数値は採用担当の実行証拠を使い、本監査からユーザーstate/cache/runtimeを再変更していない。

## 2. 現在の成果物

| Surface | Artifact | Launcher / runtime | Persistence |
| --- | --- | --- | --- |
| Browser | Next.js app `src/**`、local API、unit/E2E、Browser契約 | `start_viewer.bat`、loopback production server、`792716f...` implementation | shared Favorite/Seen/Recent/Search History、Browser `localStorage`、derived index/thumb/display/enhance cache |
| WPF | `.NET 8` WPF `local-native/PhotoViewer.Wpf/**`、aggregate verifier、WPF仕様 | `start_wpf.bat`、provenance付きRelease target、`792716f...` implementation | shared Favorite/Seen/Recent/Search History、Enhancement read、`%LOCALAPPDATA%\PhotoViewer.Wpf\state.json`、`metadata-index-v1` |
| WinForms | `.NET 8` WinForms `local-native/PhotoViewer.Native/**` と既存verification資料 | `start_winforms.bat`、`scripts/start-local-native.ps1` | native SQLite + shared JSON bridge。**FROZEN、通常gate外** |
| Canonical docs | 本書、統合正本、Browser/WPF詳細契約 | `START_HERE.md`から到達 | repo内Markdownのみ |

## 3. Browser

### 3.1 implemented

| Capability | Current result | Commit/test/runtime/launcher evidence |
| --- | --- | --- |
| local-first Browser product | Landing→scan→Viewer、loopback-only production runtime | `GIT-LOCAL-001`, `BR-SRC-001`, `BR-TEST-001`, `BR-RUN-001`, `BR-LAUNCH-001` |
| folder-set scan/index | multi-root、incremental/full、SSE progress、cancel、opaque session、recent | `BR-SRC-001`, `BR-TEST-001` |
| search/filter/sort | comma AND、filename+Prompt、Favorite/Seen/date/folder、modified/created/name/random | `BR-SRC-001`, `BR-TEST-001` |
| virtualized Grid/List | sparse paging、selection/range/arrows、Seen、bounded image caches | `BR-SRC-001`, `BR-TEST-001` |
| preview workflow | Right Preview、tabs/pins/reopen、Modal navigation/zoom/pan/flip/metadata | `BR-SRC-001`, `BR-TEST-001` |
| virtualized Modal filmstrip | current追従、direct click、viewport+overscanだけrealize、T/toolbar保存開閉、Arrow navigation、Delete隣接1回 | `ADOPT-002`, `BR-TEST-001` |
| Favorite/Seen/Recent sharing | shared JSON、malformed protection、merge/union、pending journal、cross-runtime actors | `BR-SRC-001`, prior cross-runtime gate in WPF closeout evidence |
| shared Search History | focus/click list、whole-query replacement、individual delete/Clear all、version1/max50、NFKC共通identity、protected status | `ADOPT-002`, `BR-TEST-001`, `WPF-SH-TEST-001` |
| guarded source Recycle | active-index/root/type guard、Recycle Bin only、bulk partial result | `BR-SRC-001`, `BR-TEST-001` |
| Favorite source mandatory confirmation | Favorite Lv1〜5を含むsingle/bulkは通常確認OFFでもmandatory。UIとaction boundaryでguard、Do not ask again不可 | `ADOPT-001`, `BR-TEST-001`, `BR-RUN-001` |
| enlarged-image session auto recovery | image 410を1回coalesceしViewer保持scan→fresh token。失敗時manual recovery | `ADOPT-001`, `BR-TEST-001`, `BR-RUN-001` |
| 20px〜最大1列 + Sidebar anchor | 20〜600/20 step、600=1列、persisted clamp。Sidebar開閉時も同じpath/offsetを保持 | `ADOPT-001`, unit/E2E in `BR-TEST-001`, `BR-RUN-001` |
| explicit Enhancement | explicit enqueue/retry/cancel/output delete、passive isolation、source非上書き | `BR-SRC-001`, `BR-TEST-001` |
| Enhancement EBUSY publish retry | transient renameをbounded retryし、fully-awaited copy fallback後だけsucceeded | `ADOPT-001`, outputPublish unit tests in `BR-TEST-001` |
| runtime diagnostics | source/build/dirty/buildId/port/PID safe display/copy | `BR-RUN-001`, `BR-LAUNCH-001` |

### 3.2 partial

| Capability | Present part | Missing / mismatch | Evidence |
| --- | --- | --- | --- |
| search-request session recovery | 410分類、直前結果、manual `Rescan folder set` | enlarged-image auto recoveryとは別で、search 410はmanual | `BR-SRC-001` |
| external open | guarded source open | 表示中Enhancedではなくsource固定 | `BR-SRC-001` |
| clipboard | Prompt/Negative/PNG info等text copy | Ctrl+C image bitmap/file copyなし | `BR-SRC-001` |
| multi-window session isolation | search/tags/image routeはtoken利用 | `Sidebar.tsx`のfolders fetchが`indexToken`を送らない | `BR-SRC-001` |
| suggestion contract | UIは最大8件、keyboard/ARIAあり | 既存Browser契約の最大12件と不一致 | `BR-SRC-001` |

### 3.3 pending / 実装確認待ち

| Requirement | Status | local main truth | Adoption gate |
| --- | --- | --- | --- |
| Enhancement枠色設定 | `pending` | setting/schemaなし | validation、persistence、render priority、a11y |
| 表示中Original/EnhancedをEnterで開く | `pending` | source固定 | both display states、missing output、path guard |
| 一覧EnterでModal | `実装確認待ち` | Browser source/unitには現行挙動あり。WPF parity未採用 | live keyboard/focus、cross-surface contract |
| context menu | `pending` | product context menuなし | mouse+keyboard invocation、selection、availability、Escape/focus |
| Ctrl+C画像copy | `pending` | metadata textだけ | bitmap/file contract、failure、input isolation |
| Album/collection | `pending discovery` | product model/API/UIなし | model、membership、rename/delete/recent、shared conflictを先に決定 |

### 3.4 blocked

現時点でBrowser coreを止める外部blockerは確認していない。Album/collectionは `pending discovery` で、実装が具体的な外部条件待ちになった時だけ`blocked`へ移す。

## 4. WPF

### 4.1 implemented

| Capability | Current result | Commit/test/runtime/launcher evidence |
| --- | --- | --- |
| native local viewer | Landing、multi-root scan、Viewer、Grid/List、search/filter/sort | `GIT-LOCAL-001`, `WPF-SRC-001`, `WPF-TEST-001`, `WPF-LAUNCH-001`, `WPF-RUN-001` |
| full-catalog virtualization | full ItemsSource、WrapPanel/recycling、canonical selection、tail exact | `WPF-SRC-001`, exact 100k in `WPF-TEST-001` |
| preview/Modal/tabs | Right Preview、pinned/reopen tabs、Modal wrap navigation/zoom/pan/flip/metadata | `WPF-SRC-001`, `WPF-TEST-001` |
| Favorite/Seen/Recent shared state | Browser-compatible JSON、generation writer、migration、cross-runtime stress | `WPF-SRC-001`, recorded cross-runtime closeout evidence |
| Search History shared state | background read/write、keyboard ListBox、whole-query replacement、delete/clear、protected/Busy status、ViewerState分離 | `ADOPT-002`, `WPF-SH-TEST-001` |
| Viewer state + persistent metadata index | ViewerState v2、checksummed index、cold/warm/corruption/cancel/restart | `WPF-SRC-001`, `WPF-TEST-001` |
| guarded source Recycle/open/reveal | Recycle Bin only、canonical/root/catalog/type/existence guard | `WPF-SRC-001`, `WPF-TEST-001` |
| Favorite source mandatory confirmation | Favorite Lv1〜5を含むsingle/bulkは設定OFFでもmandatory。dialog + execute boundary、Do not ask again不可 | `ADOPT-001`, `WPF-TEST-001`, `WPF-LAUNCH-001`, `WPF-RUN-001` |
| editable key bindings/accessibility foundation | conflict guard、hot apply、reload/reset、rescue keys、focus isolation | `WPF-SRC-001`, `WPF-TEST-001` |
| explicit Enhancement delegation | worker/store writeを所有せず、明示AI actionだけBrowser loopback APIへ | `WPF-SRC-001`, passive isolation in `WPF-TEST-001` |
| current launcher/runtime | Release rebuild 0 warning/error、startup 517ms、revision/content provenance match、interactive process responsive | `WPF-LAUNCH-001`, `WPF-RUN-001` |

### 4.2 partial

| Capability | Present part | Missing / mismatch | Evidence |
| --- | --- | --- | --- |
| gallery zoom | 40〜600、step 20、thumb-change zoom anchor | Browserのmin 20/600=1列 parityなし | `WPF-SRC-001` |
| Sidebar toggle | Visibility + 240/0 | reopen時のcanonical path/viewport offset anchorなし | `WPF-SRC-001` |
| external open | guarded selected source path | Modalのdisplayed Enhancedを開かない。Enter actionなし | `WPF-SRC-001` |
| clipboard | diagnostics/Prompt/Negative/PNG metadata text | image bitmap/file copyなし | `WPF-SRC-001` |
| Album surface | disabled Album button + deferred tooltip | model/membership/mutation/testなし | `WPF-SRC-001` |

WPFはlocal file viewerでBrowserのHTTP image sessionを持たないため、「拡大画像session自動回復」はWPF非該当であり、pending/blockedへ水増ししない。

### 4.3 pending / 実装確認待ち

| Requirement | Status | local main truth | Adoption gate |
| --- | --- | --- | --- |
| 20px〜最大1列 Browser parity | `pending` | 40〜600固定 | WPF XAML/state/virtual panel、anchor、100k gate |
| Sidebar開閉アンカー Browser parity | `pending` | 240/0 toggleのみ | same path/offset、repeated toggle、resize/DPI |
| Enhancement枠色設定 | `pending` | field/control/bindingなし | schema、render、persistence、high contrast |
| 表示中Original/EnhancedをEnterで開く | `pending` | source openだけ、Enterなし | E state、missing output fallback、guard |
| 一覧EnterでModal | `pending` | double-clickだけ | Grid/List Enter、current order、focus return、input isolation |
| context menu | `pending` | ContextMenu/right-button handlerなし | pointer+keyboard、selection、focus、disabled actions |
| Ctrl+C画像copy | `pending` | text copyだけ | bitmap/file contract、clipboard error、text input isolation |
| Album/collection | `pending discovery` | disabled placeholderだけ | Browserと同一ownership/modelを先に決定 |

### 4.4 blocked

現時点でWPF coreを止める外部blockerは確認していない。normal-root launcher/runtimeのcurrentnessはvolatileなPID/hashを本文へ固定せず、`WPF-LAUNCH-001`のlive verifierで判定する。

## 5. WinForms — FROZEN

| Policy | Truth | Evidence |
| --- | --- | --- |
| source/artifacts | `local-native/PhotoViewer.Native/**`、launcher、docsを残す。削除/archiveしない | `FROZEN-001`, `WIN-SRC-001` |
| feature work | 新機能、Browser/WPF追随、通常product gateの対象外 | `FROZEN-001` |
| allowed maintenance | 重大なデータ破損または起動不能だけ | `FROZEN-001` |
| compatibility | Browser/WPF shared Favorite/Seen/Recent互換を壊さない範囲だけ維持 | `NativeStateBridge.cs` static audit |
| known severe risk candidate | shared writeにBrowser/WPF同等cross-process lockが見つからず、current cross-runtime verifierにもWinForms actorなし。同時起動lost updateは未検証 | static audit。再現時だけFROZEN保守候補 |
| restart work | ユーザーが明示的にFROZEN解除した時だけ | `FROZEN-001` |

WinFormsの過去task/verificationに多数の実装記録があっても、現在のBrowser/WPF製品進捗やSection 3/4の`implemented`へ加算しない。

## 6. 今後実装予定

時期は未確定。下の順序は依存関係であり、実装済み一覧ではない。

| Order | Planned lane | Current status | Completion evidence required |
| --- | --- | --- | --- |
| 1 | WPF 20px〜最大1列 + Sidebar reopen anchor parity | pending | WPF source/state、focused layout/anchor、100k、current launcher/runtime |
| 2 | displayed Original/Enhanced Enter + WPF一覧Enter Modal | pending | input/overlay isolation、focus return、path guard、both display state |
| 3 | image context menu + Ctrl+C image copy | pending design/implementation | action model、selection、clipboard format/error、a11y |
| 4 | Enhancement枠色設定 | pending design/implementation | schema ownership、validation、persistence、visual/a11y |
| 5 | Album/collection発掘 | pending discovery | branches/history inventory、product model、shared ownership、migration/conflict |
| frozen | WinForms feature lane | FROZEN | ユーザーの明示解除なしには開始しない |

## 7. 既知問題・未確定事項

| ID | Surface | Problem / uncertainty | Effect |
| --- | --- | --- | --- |
| `KNOWN-BR-001` | Browser | `Sidebar.tsx`のfolders fetchが`indexToken`を送らない | 複数window/session完全分離のgap |
| `KNOWN-BR-002` | Browser docs | 既存Browser契約はsuggestion最大12、実装は8 | 統合正本はcurrent mainの8を採用 |
| `KNOWN-BR-003` | Browser docs | 既存source/test mapに存在しない`src/lib/modalZoom.ts`、`modalZoom.test.ts`、`dateFilter.test.ts`参照が残る | 旧契約のsource mapを単独正本にしない |
| `KNOWN-BR-004` | Browser docs | 既存契約の40〜600とmanual-only image session記述は`53dc865...`より古い | 統合正本の20〜600/1列/画像auto recoveryを優先 |
| `KNOWN-BR-005` | Browser runtime | live revisionは一致するがnormal rootはsourceDirty=true | user-owned差分をclean扱いしない。`next-env.d.ts`は不変証拠あり |
| `KNOWN-WPF-001` | WPF docs | WPF仕様先頭日、本文gate、古いparity line count/README歴史節が混在 | 統合正本と本ledgerをcurrent status優先にする |
| `KNOWN-WPF-002` | WPF parity | Browserで採用した20px/1列とSidebar reopen anchorはWPF sourceにない | Browser implementedをWPF implementedへ横流ししない |
| `KNOWN-SHARED-001` | shared JSON | Search HistoryはBrowser/WPF cross-runtime atomic gate済みだが、全storeが同一protocolまたはtransaction log/revision IDを持つわけではない | Search History以外へ今回のlost-0証拠を横流しせず、surface別recoverable statusを維持 |
| `KNOWN-WIN-001` | WinForms | Favorite/Seen/Recent writerのcross-process lock証拠なし、cross-runtime actor未参加 | FROZEN中の重大データ破損候補。通常追随では直さない |
| `KNOWN-CACHE-001` | Browser | thumb/display/enhance output disk cacheにquota/LRUなし | long-running disk growthを監視。今回cache変更なし |
| `KNOWN-TEST-001` | 共通 | Browser E2Eはcomponent/unitより狭い。本docs監査は採用担当のfull gate証拠を利用 | 新要件は専用focused/live gateが必要 |
| `KNOWN-MANUAL-001` | 共通 | screen reader、high contrast、200% DPI、OS clipboard/Recycle/shellの今回manual evidenceなし | static testだけで残要件を完了扱いしない |
| `KNOWN-GIT-001` | repo | default origin/mainはmerge前のため古いが、Draft PR #322 branchは検証済みmilestone commit `14ebe6a...` を保持する | mergeまではlocal mainとDraft PR branchを採用系列とする。新規作業を古いorigin/mainから開始せず、Actionsだけを完了gateにせず、deploymentしない |

## 8. 更新規則

1. statusを上げる時はlocal main commit、focused test、必要なruntime/launcher evidenceを同じrowへ追加する。
2. `pending adoption`はlocal mainへの採用前に`implemented`へ移さない。
3. surface片方のimplementedを他方へ横流ししない。
4. blockedへ移す時は具体的な外部条件と解除条件を書く。
5. WinFormsのFROZEN解除はユーザーの明示指示がある時だけ行う。
6. source behaviorと文書が食い違ったら、integrated specのcurrent-main差分と本ledgerを同じcommitで直す。
