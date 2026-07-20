# PhotoViewer 現在成果物・実装進捗・今後予定 truth table

最終監査日: 2026-07-20 JST

実装正本commit: Browser/WPF基盤 `792716f9863dd145e028648405c29c340f7a4336`、WPF gallery
zoom/geometry anchor採用 `e371b482af44e0428d9fe0d5217b236801f29cff`、WPF shared-state latency改善
`5ae1e00`。このmachineの`refs/heads/main`はrepository hardening `4c81cca3efc80363568d0d9af35297ff3285b48c`、
Modal/filmstrip layout `9d8acb0335392687a801504ecfcd09dda6b50be6`、Enhancement修正、表示中asset open/容量表示
`a1d83c8`、WPF primary-item focus assertion `dbad550`、Modal button Enter isolation `452ac02`、Browser shared-writer FIFO fairness `0802f95`を保持したdescendantである。

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

`implemented`へ予定を含めない。20px〜1列、Sidebar/geometry anchor、Favorite source削除確認、shared Search History、Modal filmstrip、表示中asset open/容量表示、一覧EnterはBrowser/WPF、拡大画像session回復はBrowserだけを、local main採用とgate後に昇格した。

## 1. Evidence register

| ID | 種別 | 2026-07-20までに確認した証拠 |
| --- | --- | --- |
| `GIT-LOCAL-001` | commit | task開始時local main `4c81cca3` を起点に、Modal/filmstrip、WPF zoom/anchor、shared-state latencyを保持し、Browser/WPF thumbnail設定lost-update修正`805d6c9`/`60ed739`、empty patch拒否`343a364`、表示中asset open/容量表示`a1d83c8`、WPF primary-item focus assertion `dbad550`、Modal button Enter isolation `452ac02`、Browser shared-writer FIFO fairness `0802f95`まで同系列で採用した。旧default origin/mainは実装基準にしない |
| `GIT-REMOTE-001` | provenance | default `origin/main=626b7dd5416f3619ae59fc66d47e79acd1a74fd5` はmerge前のため古いまま。Draft PR [#322](https://github.com/a9ui/tools-h000025-photoviewer/pull/322) のremote branchは、ユーザー実行のcanonical push後に検証済みmilestone commit `14ebe6a16a89123e5dfd6e966324522094c1188c` を保持した。`gh pr view` と `git ls-remote` の両方で同一SHA、`OPEN`、`isDraft=true` を2026-07-19に確認し、SQLite improvement item #45をdoneへ更新した。merge/deploymentは行っていない |
| `ADOPT-001` | adopted change | `53dc865...` はBrowserの20〜600/最大1列、Sidebar geometry anchor、Favorite source Delete guard、画像session自動回復、Enhancement output publish retryと、WPF Favorite source Delete guardを含む |
| `ADOPT-002` | adopted change | `44f9716...`〜`792716f...` はBrowser Modal virtualized filmstrip、keyboard Arrow補強、Browser/WPF shared Search History、WPF async/keyboard/a11y、cross-runtime verifier、aggregate競合防止を含む |
| `ADOPT-BR-003` | adopted Browser refinement | `9d8acb0` はModal filmstripを画像viewport外の専用下段へ置き、表示時は画像領域を縮めて重ねない。chromeは操作停止3秒で隠れ、pointer/keyboardで再表示し、Delete確認中は表示を維持する。zoom倍率indicatorは画像viewport上側中央に置く |
| `ADOPT-ENH-004` | adopted Enhancement fix | `3654b88` はdestinationへのcopy完了とtemporary cleanupを別の結果として扱い、copy成功後のtemporary `EBUSY`をjob failureへ誤分類しない。cleanupはbounded retryし、残留時は`copy-with-stale-temporary`を返す。ncnn-vulkan normalizeは書き込み直後のtemporaryをSharp metadata取得だけのために再openしない |
| `ADOPT-STATUS-005` | adopted Browser/WPF status UI | `2863519` と `a091ec7` はFavorite既定黄色2px inner、Enhanced既定虹色3px outer、独立ON/OFF、Enhanced `rainbow | #RRGGBB`、旧hex互換、shared unknown/malformed/busy非破壊を両surfaceへ採用した |
| `ADOPT-MODAL-006` | adopted Browser/WPF Modal parity | Browser `f6f63d3` とWPF `a091ec7` はmanual visible固定、manual hidden cursor、900ms transient、下端hover overlay、hidden-state navigation/Delete/Original-Enhanced維持、上端zoom、focused-button shortcutsを揃えた。`c3d4ff5`はWPF Filmstrip open stateをViewerStateへatomic保存する |
| `ADOPT-SETTINGS-007` | adopted Browser/WPF lost-update protection | Browser `805d6c9`、WPF `60ed739` はFavorite/Enhancedのdirty preference単位だけをlock内latest diskへmergeし、他surfaceの同時更新を戻さない。Browser `343a364` は空のthumbnail border patchを拒否する。unknown/malformed/future/busy保護とRetryを維持する |
| `ADOPT-DISPLAY-008` | adopted Browser/WPF displayed asset actions | `a1d83c8` はModalの現在表示中Original/Enhancedを同じresolverで容量表示とexternal openへ接続し、WPF Grid/List Enterをcurrent filtered/sorted orderのModalへ接続した。`dbad550` はModal close後のfocusをcurrent primary Grid/List itemへ戻す契約を既存P1B gateへ固定し、`452ac02`はOriginalSource/Keyboard focusの両方でModal buttonを認識してEnter/Spaceをnative activationへ隔離する |
| `ADOPT-SH-009` | adopted Browser shared-writer fairness | `0802f95` は同一Node process内の同一target writerをWindows case-insensitive keyのFIFOへ並べ、各turnで従来どおりBrowser/WPF共通create-new file lockを取得する。30並列のpoll競争による2秒starvation/503を防ぎ、cross-process/stale/live-lock timeout契約は維持する |
| `BR-SRC-001` | source audit | `docs/browser-feature-contract.md`、`src/**`、`e2e/**`、関連scripts/testsを照合。Modal filmstripとSearch History source mapを統合正本へ反映 |
| `BR-TEST-001` | adopted test | Browser unit 61 files / 521 tests、3 files / 3 tests skip、typecheck、production build、lint 0 errors、isolated production Playwright 7/7 green |
| `BR-E2E-002` | isolated production E2E | `9d8acb0`後の`e2e/viewer-modal-filmstrip.spec.ts`は`http://127.0.0.1:3001`で1/1 PASS。filmstripがimage viewport bottom以降にあり画像へ重ならないこと、zoom indicatorがstripより上にあること、3秒auto-hide/re-show、console problem 0を確認。通常port 3000はtestに使わず、3001 serverは終了後停止した |
| `BR-ENH-TEST-002` | focused Enhancement test | `3654b88`のEnhancement focused unitは4 files / 23 tests PASS。copy成功後のtransient/permanent cleanup `EBUSY`とcopy自体の失敗を分離して検証。ユーザーの既存failed job/cacheは変更していない。fresh real-GPU rerunは未実施 |
| `BR-DISPLAY-TEST-003` | focused displayed-asset test | `a1d83c8`のBrowser focused 5 files / 71 tests、typecheck、scoped lint、production buildがgreen。Original/Enhanced容量、toggle、fallback、POST-only launch、active-index/type/existence、lexical+real managed ownership、shell failureを確認した |
| `BR-CURRENT-TEST-004` | current Browser aggregate | `0802f95`後の`pnpm test:unit`は63 files / 584 tests PASS、3 files / 3 tests skip。Next.js 16.2.10 production build、shared lock + Search History focused 15/15、typecheck、scoped lint、diff-checkもgreen。同一target 30 callerはFIFO 30/30、Search History API全200、lost entry 0、lock/temp residue 0 |
| `BR-CURRENT-TEST-005` | Search History completion audit | current treeの`pnpm test:unit`は63 files / 589 tests PASS、3 files / 3 tests skip。Search History focused 2 files / 33 tests、typecheck、production build、scoped lintがgreen。遅延GET中の不可視旧entry再commitも回帰固定。isolated `127.0.0.1:31337` Playwright 1/1はinput/row Arrow/Enter/Escape、manual/900ms/overlay Filmstrip、遠方移動、Delete一段移動、console problem 0を実Browserで確認し、port 3000を使用していない |
| `BR-RUN-001` | normal launcher adoption | completion-audit commit `e6cf850`時点で、通常WPF Release targetはbuild + provenance record後に`current / provenance-match`、source revision `e6cf850`。通常Browser `127.0.0.1:3000`はユーザー使用中のroot production launcherで、loopback/ownerは正しいがruntime revisionは旧`dbad550`。稼働中buildを壊さないため停止・in-place buildを行っておらず、`e6cf850`採用にはユーザー都合で通常launcher再起動が必要。build ID/PIDはvolatileで正本値にしない |
| `BR-GIT-005` | Draft PR publication boundary | GitHubの唯一のopen PR #322はDraft/CLEAN、remote head `14ebe6a`。local `main`は`e6cf850`で、`git push origin main:codex/wpf-thumbnail-performance-20260719`はGitHub権限ではなくCodexローカル実行ポリシーによりprocess開始前に拒否。PR comment `5018114574`へ証拠と手動push待ちを記録し、迂回API uploadは行っていない |
| `BR-LAUNCH-001` | launcher | `start_viewer.bat` → `scripts/prod_launcher.js`。revision/build/port/PID provenance、loopback bind、owned process tree、explicit busy port refusal |
| `WPF-SRC-001` | source audit | `docs/wpf-product-spec.md`、XAML、code-behind、support classes、verifier inventoryをlocal mainで照合 |
| `WPF-TEST-001` | adopted test | zoom promotion時のcurrent-main aggregate + reload soak 53/53、reload 24/24がgreen。exact 100,000 images / 100 foldersはcatalog/filtered/Grid各100,000、silent truncate 0、Grid/List 15/9、tail 99,999、20/600 endpoints、600=1列、全anchor drift 0、warm hit 100,000 / miss 0、最大unresponsive 262ms / gate 750ms。その後のshared-state latency descendantは`-SkipStress` aggregate 51/51、focused latency 6/6、Release rebuild 0 warnings / 0 errors |
| `WPF-SH-TEST-001` | adopted test | Search History focusedはasync UI、handler相当keyboard helper + compiled XAML wiring、a11y/refocus/Unicode/live-lock Busy writes 0 green。Browser/WPF 20+20 concurrentは42 entries、lost 0、unknown field保持、malformed/future保護、lock/tmp 0。実OS routed-key eventのmanual証拠ではない |
| `WPF-SH-TEST-002` | completion-audit strengthened gate | rawとnormalized queryを各32,768 UTF-16 code unit以下へ固定し、U+FEFF/U+0085を含む明示trim集合をBrowser/.NETで共有。cross-runtime verifierは両worker ready後だけstart gateを開き、20+20 loopの実interval overlap 592〜668ms、43 final entries、lost 0、Busy 0、unknown/malformed/future/lock/temp保護を確認した。current `verify-wpf-product.ps1 -IncludeReloadSoak`は55/55、462,762ms、reload 24/24 green |
| `WPF-ZOOM-001` | adopted change + acceptance | candidate `4a22b61`をblind cherry-pickせず、task開始時main `4c81cca3`、先行Modal/filmstrip `9d8acb0`、以後のmain変更を保持して `e371b48` へsemantic adoptionした。20〜600/step 20、600=1列、旧40維持・範囲外clamp、canonical full path+viewport offsetをzoom/Sidebar/right panel/window resize/DPI/selection有無で保持する。focused、current aggregate 53/53 + reload soak 24/24、exact 100,000/100 folders、Search History focused/stallがgreen |
| `WPF-DISPLAY-002` | adopted displayed-asset/list-enter acceptance | `verify-wpf-external-open.ps1`、`verify-wpf-gallery-enter-modal.ps1`、`verify-wpf-modal-enhancement-actions.ps1`、`verify-wpf-modal-interaction.ps1`、`verify-wpf-thumbnail-status-borders.ps1`が全green、Release build 0 warnings / 0 errors。Original/Enhanced target/容量、stale/missing/ownership fallback、ShellExecute failure、Grid/List current order、navigation後focus、input/overlay/Landing isolationを確認した |
| `WPF-DISPLAY-AGG-003` | current displayed-asset aggregate | 12 owned feature blobsがcheckpoint `8ff1e52`とcurrent main `0802f95`でbyte-identical。`verify-wpf-product.ps1 -IncludeReloadSoak`は55/55、`AGGREGATE_EXIT=0`、333,899ms、reload soak 24 cycles / 40,322ms。Prompt-tag Enter isolationと新gallery-enter verifierを含む。log SHA-256は`B9D355CE876D5E1B15CE8B1FC8F95E5800B9238301E5D301BBC1034F70ABA0BC`。layout/state/stress実装・verifierは変更していないためexact 100,000/100 foldersは`WPF-ZOOM-001`のcurrent-main証拠を継承する |
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
| WPF | `.NET 8` WPF `local-native/PhotoViewer.Wpf/**`、aggregate verifier、WPF仕様 | `start_wpf.bat`、provenance付きRelease target、`e371b48...` zoom/anchor + `5ae1e00` shared-state latency implementation | shared Favorite/Seen/Recent/Search History、Enhancement read、`%LOCALAPPDATA%\PhotoViewer.Wpf\state.json`、`metadata-index-v1` |
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
| virtualized Modal filmstrip | current追従、direct click、viewport+overscanだけrealize、T/toolbar保存開閉、Arrow navigation、Delete隣接1回。manual時は専用下段、hidden下端hoverは前面overlay。manual visible固定、hidden cursor + 900ms transient、hidden-state維持、root上端zoom | `ADOPT-002`, `ADOPT-BR-003`, `ADOPT-MODAL-006`, `BR-TEST-001` |
| displayed asset open / capacity | Modalに現在表示中assetの実体容量を1024² bytes基準、小数2桁・spaceなしのexact `0.00MB`形式で表示。Originalはsource、Enhancedはmanaged output。E toggleで即更新し、missing/stale/invalid signature/ownership外はOriginal容量へfallback + recoverable status。Enterは同じ表示中assetをguardして開く | `ADOPT-DISPLAY-008`, `BR-DISPLAY-TEST-003` |
| thumbnail status borders | Favorite黄色inner、Enhanced虹色outer、独立ON/OFF、rainbow/単色、Grid/List同時表示、dirty preference単位latest-disk merge、空patch拒否、shared保存保護 | `ADOPT-STATUS-005`, `ADOPT-SETTINGS-007` |
| Favorite/Seen/Recent sharing | shared JSON、malformed protection、merge/union、pending journal、cross-runtime actors | `BR-SRC-001`, prior cross-runtime gate in WPF closeout evidence |
| shared Search History | focus/click list、whole-query replacement、individual delete/Clear all、version1/max50、NFKC共通identity、protected status | `ADOPT-002`, `BR-TEST-001`, `WPF-SH-TEST-001` |
| guarded source Recycle | active-index/root/type guard、Recycle Bin only、bulk partial result | `BR-SRC-001`, `BR-TEST-001` |
| Favorite source mandatory confirmation | Favorite Lv1〜5を含むsingle/bulkは通常確認OFFでもmandatory。UIとaction boundaryでguard、Do not ask again不可 | `ADOPT-001`, `BR-TEST-001`, `BR-RUN-001` |
| enlarged-image session auto recovery | image 410を1回coalesceしViewer保持scan→fresh token。失敗時manual recovery | `ADOPT-001`, `BR-TEST-001`, `BR-RUN-001` |
| 20px〜最大1列 + Sidebar anchor | 20〜600/20 step、600=1列、persisted clamp。Sidebar開閉時も同じpath/offsetを保持 | `ADOPT-001`, unit/E2E in `BR-TEST-001`, `BR-RUN-001` |
| explicit Enhancement | explicit enqueue/retry/cancel/output delete、passive isolation、source非上書き | `BR-SRC-001`, `BR-TEST-001` |
| Enhancement EBUSY publish retry | transient renameをbounded retryし、fully-awaited copy fallback後だけsucceeded。copy完了後のtemporary cleanupは別処理で、cleanup lockを完成済みoutputの失敗へ誤分類しない | `ADOPT-001`, `ADOPT-ENH-004`, `BR-TEST-001`, `BR-ENH-TEST-002` |
| runtime diagnostics | source/build/dirty/buildId/port/PID safe display/copy | `BR-RUN-001`, `BR-LAUNCH-001` |

### 3.2 partial

| Capability | Present part | Missing / mismatch | Evidence |
| --- | --- | --- | --- |
| search-request session recovery | 410分類、直前結果、manual `Rescan folder set` | enlarged-image auto recoveryとは別で、search 410はmanual | `BR-SRC-001` |
| clipboard | Prompt/Negative/PNG info等text copy | Ctrl+C image bitmap/file copyなし | `BR-SRC-001` |
| multi-window session isolation | search/tags/image routeはtoken利用 | `Sidebar.tsx`のfolders fetchが`indexToken`を送らない | `BR-SRC-001` |
| suggestion contract | UIは最大8件、keyboard/ARIAあり | 既存Browser契約の最大12件と不一致 | `BR-SRC-001` |

### 3.3 pending / 実装確認待ち

| Requirement | Status | local main truth | Adoption gate |
| --- | --- | --- | --- |
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
| gallery zoom 20px〜最大1列 | 20〜600、step 20、600=1列、旧40維持・範囲外clamp、Grid/List virtualization bounded | `WPF-ZOOM-001`, `WPF-TEST-001` |
| gallery geometry anchor | selected canonical full path優先、selectionなしfallback。zoom、Sidebar、right panel、window resize、DPIでviewport offsetを維持 | `WPF-ZOOM-001`, `WPF-TEST-001` |
| Modal manual/transient UI + Filmstrip | manual visible固定、hidden cursor + 900ms transient、専用bottom row、下端hover overlay、button-focus shortcut、hidden-state維持、ViewerState保存 | `ADOPT-MODAL-006`、`verify-wpf-modal-interaction.ps1`全true |
| Grid/List Enter → Modal + focus return | current filtered/sorted orderのprimary itemをEnterでModalへ開く。navigation後に閉じてもcurrent primary Grid/List itemへfocusを戻し、Search/Date/Settings/Delete/Modal native input/Landingでは発火しない | `ADOPT-DISPLAY-008`, `WPF-DISPLAY-002` |
| displayed asset open / capacity | ModalのOriginal/Enhanced実体容量をexact `0.00MB`表示し、E toggleで即更新。missing/stale/invalid signature/ownership外EnhancedはOriginal容量 + recoverable statusへfallback。Enterは同じ表示中assetをcanonical guard後にShellExecuteする | `ADOPT-DISPLAY-008`, `WPF-DISPLAY-002` |
| thumbnail status borders | Browser同一schema、Favorite黄inner、Enhanced虹outer、独立ON/OFF/単色、Freeze済みshared brush、Grid/List O(1)、dirty preference単位latest-disk merge | `ADOPT-STATUS-005`, `ADOPT-SETTINGS-007`、`verify-wpf-thumbnail-status-borders.ps1`全true |

### 4.2 partial

| Capability | Present part | Missing / mismatch | Evidence |
| --- | --- | --- | --- |
| clipboard | diagnostics/Prompt/Negative/PNG metadata text | image bitmap/file copyなし | `WPF-SRC-001` |
| Album surface | disabled Album button + deferred tooltip | model/membership/mutation/testなし | `WPF-SRC-001` |

WPFはlocal file viewerでBrowserのHTTP image sessionを持たないため、「拡大画像session自動回復」はWPF非該当であり、pending/blockedへ水増ししない。

### 4.3 pending / 実装確認待ち

| Requirement | Status | local main truth | Adoption gate |
| --- | --- | --- | --- |
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
| 1 | image context menu + Ctrl+C image copy | pending design/implementation | action model、selection、clipboard format/error、a11y |
| 2 | Album/collection発掘 | pending discovery | branches/history inventory、product model、shared ownership、migration/conflict |
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
| `KNOWN-GIT-002` | repo | task開始後も`refs/heads/main`が`4c81cca3`からModal/filmstrip、WPF zoom、Enhancement、shared-state latencyへ進んだ | stale snapshotへ固定せず、`4a22b61`をblind cherry-pickしない。`e371b48`以後のcurrent-main treeとgateを正本にする |
| `KNOWN-SHARED-001` | shared JSON | Search HistoryはBrowser/WPF cross-runtime atomic gate済みだが、全storeが同一protocolまたはtransaction log/revision IDを持つわけではない | Search History以外へ今回のlost-0証拠を横流しせず、surface別recoverable statusを維持 |
| `KNOWN-WIN-001` | WinForms | Favorite/Seen/Recent writerのcross-process lock証拠なし、cross-runtime actor未参加 | FROZEN中の重大データ破損候補。通常追随では直さない |
| `KNOWN-CACHE-001` | Browser | thumb/display/enhance output disk cacheにquota/LRUなし | long-running disk growthを監視。今回cache変更なし |
| `KNOWN-ENH-001` | Browser Enhancement runtime | `3654b88`のpublish/cleanup分類とtemporary再open回避はfocused 4 files / 23 testsでgreenだが、同machineのfresh real-GPU jobはまだ再実行していない | 既存failed job/cacheを削除せず、次の明示Enhance actionでdestination生成、job succeeded、表示decode、temporary residue/warningを確認する |
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
