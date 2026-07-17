# PhotoViewer 製品品質レビュー基準

文書状態: **Normative review framework / Browser・WPF共通**

制定日: 2026-07-18 JST

適用範囲: local-first Browser版とWindows WPF版。公開service、deployment、cloud/accountは対象外。

## 1. 目的と正本関係

この文書は、PhotoViewerを継続的にレビューし、次にどこへ開発力を使うかを同じ基準で決めるための品質gateである。単なる印象評価ではなく、exact commit、再現fixture、focused verifier、aggregate gate、runtime証拠を使って採点する。

製品の意味を新しく定義する文書ではない。仕様が衝突した場合は次の順で判断する。

1. [browser-feature-contract.md](./browser-feature-contract.md) のMUSTと安全契約。
2. [wpf-product-spec.md](./wpf-product-spec.md) のWPF native契約。
3. [browser-to-wpf-parity-plan.md](./browser-to-wpf-parity-plan.md) のlive completion ledgerとDEFER境界。
4. focused verifierとlive code。
5. この文書の採点・優先順位規則。

[ai-implementation-brief.md](./ai-implementation-brief.md) は新規実装者の入口、[product-review-20260718.md](./product-review-20260718.md) はある時点の評価snapshotである。この文書は、snapshotを作り直す時の恒常的な判定方法を定める。

`local-native/ui-mockup/**`、過去README、古いGitHub snapshot、固定された過去のtest件数は採点根拠にしない。

## 2. 判定原則

### QR-PRINCIPLE-001 Evidence first

- 採点対象のexact commit、branch、dirty state、実行日時を必ず記録する。
- 各軸は、主張したい点数ではなく「必要証拠をすべて満たした最も高い段階」で採点する。
- live evidenceがない軸は最大1点、manual確認だけなら最大2点、focused verifierだけなら最大3点、exact commitのaggregateまで通った場合に最大4点とする。
- 5点は、4点の条件に加えて、独立した複数run、長期stressまたは代表的なfault injectionで同じ結果を再現した時だけ付ける。
- test数はsuite inventoryの観測値であり固定要件ではない。0 failureを要求し、passed/skipped数とskip理由を記録する。
- screenshotだけ、文書だけ、mockだけ、fileの存在だけでは機能合格にしない。

### QR-PRINCIPLE-002 Safety cannot be averaged away

総合点が高くても、hard no-go、未解決P0、またはデータ非破壊が2点以下ならrelease不可とする。視覚品質、機能数、速度でsource/state破壊や誤Deleteを相殺してはならない。

### QR-PRINCIPLE-003 Preserve meaning, improve implementation

Browserは製品意味の正本である。WPFはDOM/CSS/HTTPを複製する必要はないが、Favorite、Seen、Delete、filter、zoom、selection、Enhancement isolationの意味と結果を変えない。WPF native extensionはBrowser契約を壊さず、既定UIを圧迫せず、temp-only verifierを持つ時だけ採用できる。

Browserの`CURRENT LIMITATION`は互換要件ではない。WPFが安全に改善できる場合は、同じ欠陥を再現しない。

### QR-PRINCIPLE-004 No destructive migration

state/cache/shared JSONを全削除してtestやmigrationを通すことを禁止する。malformed/future fileはbyte-preserveし、書込みを拒否してrecoverable statusを出す。Delete testは専用temp copy以外へ実行しない。

### QR-PRINCIPLE-005 One bounded upgrade at a time

改善は、再現可能な欠陥または測定可能な利用価値を1件ずつ閉じる。全面rewrite、根拠のないUI刷新、複数の所有境界を同時に変える変更は、個別の最小修正で解決不能な証拠がない限り採用しない。

## 3. Severity

findingは次のどれか一つへ分類する。severityは「直しにくさ」ではなく、利用者とデータへ与える影響で決める。

| Severity | 定義 | 代表例 | 扱い |
| --- | --- | --- | --- |
| **P0 Critical** | source/state破壊、誤った破壊操作、primary workflowのcrash/hang、trust/process ownership違反、中心的製品意味の逆戻り。安全な回避策がない、または利用者が欠陥を事前判別できない | hard-delete fallback、active root外Delete、Favorite/Seen lost update、silent catalog truncate、passive Enhancement開始、旧/別worktree binary配信、zoomでsidebarまで縮小、Delete後の無関係画像移動 | 即時no-go。新機能を止め、最小修正と専用回帰を先に行う |
| **P1 Major** | 日常flowの正確性、回復性、性能、keyboard/focusを大きく損なう。データ破壊は起きないが、頻繁な失敗または明示的な回避操作が必要 | cancel後のstale publish、decode失敗で旧画像を成功表示、reloadで主要state消失、主要操作がkeyboardで完了不能、5,000件で実用不能なstall | 同一milestoneで修正。closeout時に未解決ならcommercial readiness不可 |
| **P2 Moderate / Upgrade** | core resultは正しいが、効率、分かりやすさ、稀なedge、visual polish、native advantageが不足する | 操作step過多、secondary actionのfocus polish、追加stress observability、非破壊的native shortcut | P0/P1が0件の時に優先式で順位付けし、bounded sliceとして採用 |

`P3/DEFER`はseverityではなく製品判断境界である。WPF Enhancement write ownership、cache quota、installer/signingなど、現行scope外のものをP0〜P2欠陥へ読み替えない。

severity判断が割れる場合は、次の順で高い方を採用する。

1. real source/shared state/process ownershipへ影響するか。
2. primary workflowを完了できるか。
3. 利用者が実行前に危険を理解して回避できるか。
4. 再現頻度と影響範囲。
5. 自動回復か、手動restart/repairが必要か。

## 4. Hard no-go

次のいずれかが観測された候補buildは、総合点に関係なく**NO-GO**である。

### 4.1 製品no-go

1. source DeleteがWindows Recycle Bin以外へfallbackする、またはcanonical active-root/current-catalog/supported-type/project・app-root guardを再検証しない。
2. test、migration、起動、scan、reloadの都合でreal user source、state、Favorite、Seen、Recent、cacheを削除・初期化・空上書きする。
3. malformed/future shared file、lock timeout、write failureで既存bytesを失う。
4. Browser/WPF/third writerの競合で無関係path、unknown field、additive Seen、recent historyをlost updateする。
5. scan、scroll、preview、modal、tab hover等のpassive workflowがEnhancement jobをenqueueする、workerを起動する、またはjobs fileを変更する。
6. catalogを無言でtruncateし、全件modelとvisual realization上限を混同する。
7. Favorite Lv1〜5 exact multi-toggle + All、Unseen dots表示専用/既定OFF、Folders collapse、manual From/To、Delete neighbor、gallery zoom isolation/anchorのいずれかを旧仕様へ戻す。
8. `Quick Search`、`Today`、`7d`、`30d`、`This year`、Favorite threshold UIを正規UIへ再導入する。
9. current decode/search/scanより古いcompletionが最新selection、preview、Modal、catalog、stateを上書きする。
10. launcherが別worktree、別revision、source drift、改変exeをcurrentとして起動する。または既存WPF process、Browser server、所有していないprocess treeを停止する。
11. Browser production listenerを非loopback interfaceへbindし、local absolute-path APIを外部へ露出する。
12. release対象のprimary flowで再現可能なunhandled exception、deadlock、無期限UI freezeがある。

### 4.2 レビュー無効化

次の場合は製品欠陥と断定せず、証拠packを無効として取り直す。

- exact commit/dirty stateを記録していない。
- Browser runtime provenanceまたはWPF launcher provenanceが対象sourceと一致しない。
- ユーザー利用中のport 3000を停止・再build・所有変更した。
- 自分が開始したexact launcher PID/direct child tree以外をport番号やprocess名だけで停止した。
- Browserのbuild/typecheckを同時実行して`.next/types`を競合させた。
- 同じWPF projectを複数processで同時buildして`obj/bin`を競合させた。
- Delete verifierがdisposable copyであることを証明できない。
- user state/cache/sourceのbefore/after hashまたは明示的非接触証拠がない。

## 5. 8軸の0〜5採点

総合点は各軸の加重平均で算出するが、Section 4のno-goとSection 8の最低点条件が常に優先する。

### 5.1 データ非破壊 — weight 20%

対象: source、Favorite、Seen、Recent、ViewerState、preview tab state、Enhancement jobs/output、cache、atomic temp/lock。

| Score | 到達条件 |
| --- | --- |
| **0** | real user dataの削除・破損・空上書き、hard delete、guard外Recycle、または再現可能なlost updateがある |
| **1** | 直接write/deleteの所有境界が不明。happy path以外の証拠がなく、malformed/lock/partial failureで保持を保証できない |
| **2** | 基本guardとtemp fixtureはあるが、atomic write、latest-disk merge、rollback、unknown field、cross-process競合のいずれかが未証明 |
| **3** | Deleteはtemp-only + Recycle-only +直前guard。各storeは正常系でatomic、失敗時に元bytesを保持し、影響workflowのfocused verifierが通る |
| **4** | malformed/future、fresh/stale lock、crash residue、partial failure、Browser/WPF同時writer、additive Seen、Recent ownership、Delete後UI ownershipをfault fixtureで証明。source/shared storeのbefore/after hashが期待どおり |
| **5** | 4点に加え、actual process abrupt-exit、複数writer反復、Delete/Refresh/decode race等を複数runで再現し、unrelated entries・unknown fields・real user data非接触・residue 0を継続証明 |

必須証拠:

- disposable fixture rootとoverride state/store path。
- source/store/hashのbefore/after、lock/tmp residue。
- Delete backendの種類とreal Delete件数。
- shared writerのruntime、iteration、expected/actual entry数。
- malformed/future/fresh lock時の元bytes保持。

この軸のhard fail: permanent delete、guard外Delete、user data初期化、lost update、malformed overwrite。

### 5.2 操作正確性 — weight 20%

対象: scan、search/filter/sort、selection、Favorite/Seen、zoom、preview/tabs、Modal、Delete neighbor、bulk partial result。

| Score | 到達条件 |
| --- | --- |
| **0** | primary workflowが完了しない、または中心的意味が旧仕様/別仕様へ戻っている |
| **1** | 一部画面は動くが、同じ操作がsurfaceや状態によって異なる結果になる。期待order/stateを説明できない |
| **2** | happy pathは動くが、filter境界、sparse result、bulk partial failure、reload、端点、keyboard等で結果がずれる |
| **3** | 主要flowを契約IDどおり完了し、各操作のstate transition、order、empty/error結果をfocused verifierで固定 |
| **4** | rapid input、filter外tab、virtualization、reload、race、bulk、sparse orderを含めても最後の意図だけがcommitされ、全surfaceを共通reconciliationする。aggregateが0 failure |
| **5** | 4点に加え、長い代表操作sequenceと順序/property fixtureを複数runで通し、Grid/List/preview/modal/keyboard/pointerが同じ意味へ収束する |

必須証拠:

- Favorite 0〜5、Unrated、Lv複数選択、Allの期待件数。
- Seenを付ける操作/付けない操作とdots ON/OFF時の同一Seen data。
- Delete cancel/middle/last/only/filter/sparse/bulk partialのneighbor結果。
- zoom前後のsidebar/header/right panel寸法とanchor drift。
- selection、tab order/active/pin、Modal order、reload後state。

この軸のhard fail: exact Favorite semantics、Seen/dots分離、Delete neighbor、zoom isolation/anchor、retired UIのいずれかの逆戻り。

### 5.3 退行耐性 — weight 15%

対象: specification traceability、static gate、unit/component/focused verifier、aggregate、runtime smoke、suite inventory。

| Score | 到達条件 |
| --- | --- |
| **0** | 既知回帰が残る、または変更対象にtest/verifierがない |
| **1** | manual確認または古いscreenshotだけ。exact sourceと期待結果が結び付かない |
| **2** | unit/focused testの一部はあるが、修正前RED、境界条件、aggregate、runtimeのいずれかを欠く |
| **3** | 欠陥を修正前に再現し、最小focused verifierを追加。関連static/build gateがexact commitで通る |
| **4** | Browser/WPFの該当aggregate、cross-runtime/safety gate、runtime provenance、UI regression guardが0 failure。仕様・source map・test mapを同じchangeで更新 |
| **5** | 4点に加え、複数run/soak/fault replayでflakyでないことを証明し、verifier inventoryの自動検出、意図的skip理由、過去再発欠陥のcharacterizationを維持 |

必須証拠:

- 修正前REDと修正後GREENの同一fixture、または新規機能なら明示された受入fixture。
- exact command、exit code、passed/skipped、duration。
- Browser unit/typecheck/build/lint、WPF Release/aggregate/cross-runtimeの影響範囲に応じた結果。
- runtime/exe provenance、console/error/warning。
- 変更した契約IDとsource/test map。

この軸のhard fail: failing release gateを既知のまま合格扱い、test削除による固定件数合わせ、別commit/binaryの証拠流用。

### 5.4 大規模性能 — weight 10%

対象: full catalog、virtualization、decode bounds、search/selection latency、cancellation、memory/working set、UI heartbeat。

| Score | 到達条件 |
| --- | --- |
| **0** | silent truncate、OOM、primary UI hang、無制限decode/container materializationが再現する |
| **1** | 小規模fixtureだけで、件数・realization・memory・latencyを測っていない |
| **2** | 全件modelはあるが、大量fixtureでbounded UI、tail item、stale cancellation、decode envelopeのいずれかが未証明 |
| **3** | 5,000件相当でexact count、tail search/preview/modal、bounded realization、UI heartbeatを記録し、silent truncate 0 |
| **4** | WPFは20,000件stress、Browserはvirtualized/paged large-library protocolを通し、decode pixel/long-edge bound、rapid stale discard、24-cycle reload envelope等の該当gateが合格。時間/WS/GCを観測値として保存 |
| **5** | 4点に加え、固定machine/build/fixtureの複数baselineでcold/warm、p50/p95、長期soak、memory plateau傾向を比較し、採用済みbudgetまたは有意な改善を反復証明 |

必須証拠:

- fixture total/indexed/filtered、tail identity、truncate count。
- Grid/List realized/container上限。
- fixture作成、load、search、selection-to-preview、heartbeat。
- start/end/peak working set、managed heap、GC、cycle sample。
- source countとEnhancement jobs hash不変。

hardware/build/fixtureが固定されていない単発時間をhard pass閾値にしない。correctness envelopeと性能trendを分けて判定する。

この軸のhard fail: silent cap、unbounded source-size decode、stale completionによるwrong image、代表件数で操作不能なfreeze。

### 5.5 失敗回復 — weight 15%

対象: scan cancel/partial failure、missing/locked/corrupt/replaced source、search/API failure、persistence refusal、crash lock、retry/focus/state reconciliation。

| Score | 到達条件 |
| --- | --- |
| **0** | recoverable faultでapp crash、source/store mutation、stale success表示、または操作不能になる |
| **1** | silent catch、blocking dialog、再起動だけが回復手段。原因と継続可否が分からない |
| **2** | errorは表示するが、valid result保持、cancel、retry、focus、mixed warning、latest-generation guardのいずれかが不整合 |
| **3** | 個別faultをinline recoverable statusへ出し、正常画像/valid root/既存stateを保持。cancel/retryが可能で、stale completionを拒否するfocused verifierが通る |
| **4** | partial multi-root、materialization TOCTOU、decode mutation/lock、Delete/Refresh、malformed/contended store、abrupt exitをfault injectionし、selection/tab/Modal/focus/stateをsurvivorへ整合。aggregateが合格 |
| **5** | 4点に加え、複数faultの同時発生、反復crash/restart、長期reload/cancel/supersedeを通し、回復後の再試行、residue 0、エラーsurfaceの優先順位を継続証明 |

必須証拠:

- injected fault、発生phase、expected/actual status。
- survivor catalog、selection、preview tab、Modal、focus、retry結果。
- cancel/supersede/stale completion/CTS ownership数。
- fault前後のsource/shared store/hash、tmp/lock residue。
- warningが後続warningで消されず、復旧actionを保持する証拠。

この軸のhard fail: recoverable IO faultでdispatcher/app crash、失敗後に旧bitmapを成功表示、cancel runのpartial publish、stale runのnew state上書き。

### 5.6 Accessibility — weight 5%

対象: keyboard-only primary flow、focus order/trap/return、Automation/ARIA name・state、live status、small viewport、disabled reason。

| Score | 到達条件 |
| --- | --- |
| **0** | primary actionがpointer-only、keyboard trap、focus消失により主要flowを完了できない |
| **1** | native/semantic controlが少なく、名前・状態・focus順が不明 |
| **2** | 一部label/shortcutはあるが、dialog initial focus、trap、Escape、return、live errorのいずれかが欠ける |
| **3** | Landing→Viewer→filter→selection→preview→Modal→Settings→Delete確認をkeyboardで完了し、主要controlがaccessible name/stateを公開 |
| **4** | dialog/overlayのTab・Shift+Tab・Control+Tab cycle、focused child上のEscape、opener fallback、edge zone/metadata/copy/resize/bulk statusをfocused automationで証明。small viewportでも主要操作に到達可能 |
| **5** | 4点に加え、代表的なUI Automation treeとscreen reader/high contrast/scale環境を複数状態で確認し、primary flowの理解可能性とfocus visibilityを反復証明 |

必須証拠:

- keyboard-only sequenceと各stepのfocused element。
- dialog role/name、AutomationName/HelpTextまたはARIA role/state。
- open/close/cancel/error後のfocus return。
- disabled actionの理由、live statusの読み上げ対象。
- desktop/small viewportまたはDPI条件のscreenshotと機能確認。

この軸のhard fail: Delete/confirm/Modal close等の安全上重要な操作がkeyboardで到達不能、top overlayから背面へfocusが漏れて誤操作可能。

### 5.7 起動終了品質 — weight 5%

対象: exact source/binary/runtime provenance、loopback/process ownership、build freshness、state close flush、crash recovery、window lifecycle。

| Score | 到達条件 |
| --- | --- |
| **0** | stale/別root binaryを起動、無関係processをkill、非loopback公開、close時にstate破損がある |
| **1** | appは開くが、source revision、binary hash、owner、bind、終了処理を証明できない |
| **2** | 通常起動/終了はできるが、freshness fail-closed、exact ownership、close flush、crash pathのいずれかが未証明 |
| **3** | 正規launcherから対象rootを起動し、user port/process非干渉、正常終了、最終state保存、残留listener/processなしを確認 |
| **4** | Browserはrevision/build ID/loopback/owned child tree、WPFはrepo/project/target/revision/source fingerprint/exe SHA256を照合し、stale/invalid/改変時fail-closed。abrupt exitとfresh/stale lock回復を含む |
| **5** | 4点に加え、cold/warm/rebuild/direct-launch、repeated open/close/crash、monitor/DPI/topology等の代表環境を反復し、起動時間、shutdown residue、復旧結果をbaseline化 |

必須証拠:

- launcher path、cwd、revision、dirty state、build ID/source fingerprint、exe hash。
- listener address/port/PIDまたはWPF process identity。
- user port 3000 owner before/after。
- launcherが停止したexact PID/direct child tree。
- close時のfinal state、pending async cancellation、残留process/lock/tmp。

private-local reviewではinstaller、signing、auto-updateをこの軸の4点条件に含めない。public distributionを判断する時だけ別gateを追加する。

この軸のhard fail: wrong/stale provenance、non-loopback bind、unowned process stop、closeで最終state喪失またはshared store副作用。

### 5.8 Browser意味一致 — weight 10%

対象: Browser正本のMUST、WPF native adaptation、cross-runtime shared state、明示DEFER、CURRENT LIMITATIONの非複製。

| Score | 到達条件 |
| --- | --- |
| **0** | Favorite/Seen/Delete/zoom/retired UI等の中心意味がBrowser正本と矛盾する |
| **1** | 見た目だけを模倣し、結果・既定値・state・安全境界が対応付いていない |
| **2** | 多くのflowは近いが、少なくとも一つのMUST、shared schema、reload結果、native差分が未決定 |
| **3** | BrowserのMUST結果をWPF native操作へ明示mappingし、差分をADOPT/ADAPT/ADD/NATIVE-EXTENSION/DEFER/DROPで分類。focused parity verifierが通る |
| **4** | cross-runtime Favorite/Seen/Recent、Delete ownership、large catalog、reload、visual stateを同じfixtureで確認。Browser CURRENT LIMITATIONをWPFへ欠陥として複製せず、native extensionが意味を変えない |
| **5** | 4点に加え、Browser仕様変更ごとのbidirectional impact reviewとdifferential fixtureを運用し、WPF固有改善がBrowser契約を保ったまま測定可能な価値を継続的に上回る |

必須証拠:

- 対応する`BR-*` / `WPF-*`契約IDとparity分類。
- 同じfixtureに対するBrowser/WPFのexpected/actual result。
- Favorite/Seen/Recent cross-runtime merge。
- 同一viewport/stateのvisual比較とnative差分理由。
- DEFER/NON-GOAL/CURRENT LIMITATIONを混同していないfeature ledger。

この軸のhard fail: old Browser/WPF資料から削除済みUIやthreshold semanticsを復活、Browserの欠陥を互換要件として複製、shared stateを別意味でwrite。

## 6. 総合点

各軸を0〜5で採点し、次の式を使う。

```text
Overall =
  DataNonDestructive * 0.20 +
  OperationCorrectness * 0.20 +
  RegressionResistance * 0.15 +
  LargeScalePerformance * 0.10 +
  FailureRecovery * 0.15 +
  Accessibility * 0.05 +
  StartupShutdown * 0.05 +
  BrowserSemanticParity * 0.10
```

小数第2位まで記録する。平均点だけで合否を決めず、各軸の最低点、open severity、hard no-goを併記する。

レビューscorecard:

| Axis | Weight | Score | Evidence | Open finding |
| --- | ---: | ---: | --- | --- |
| データ非破壊 | 20 |  |  |  |
| 操作正確性 | 20 |  |  |  |
| 退行耐性 | 15 |  |  |  |
| 大規模性能 | 10 |  |  |  |
| 失敗回復 | 15 |  |  |  |
| Accessibility | 5 |  |  |  |
| 起動終了品質 | 5 |  |  |  |
| Browser意味一致 | 10 |  |  |  |
| **Overall** | **100** |  |  |  |

## 7. 必須evidence pack

milestone closeoutまたはcommercial readiness判定では次を1組で残す。

### 7.1 Identity

- exact integrated commit、branch、dirty state。
- Browser build/runtime revision、build ID、host、port、launcher/server PID。
- WPF source fingerprint、Release exe SHA256、ProductVersion、launcher判定。
- 実行日時、machine/runtime version、主要fixture identity。

### 7.2 Static and aggregate

Browser:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-ui-regression-guard.ps1
pnpm test:unit
pnpm typecheck
pnpm build
pnpm lint
```

WPF:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-ui-regression-guard.ps1
dotnet build .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-product.ps1 -IncludeReloadSoak
powershell -ExecutionPolicy Bypass -File .\scripts\verify-cross-runtime-shared-state.ps1 -Iterations 20
powershell -ExecutionPolicy Bypass -File .\scripts\verify-cross-runtime-recent.ps1 -Iterations 20
```

実行時のlive check/test数、passed/skipped、duration、0 failureを記録する。WPF closeoutは`-SkipStress`を使わない。

### 7.3 Runtime and interaction

- Browserはuser portとは別のfree portで通常production launcherを使い、`verify-browser-runtime.ps1`でexact revisionとloopbackを確認する。
- WPFはproject rootの`start_wpf.bat`からfreshness分岐を確認する。
- Landing→scan→search/filter→zoom→selection→preview tabs→Modal→Settings→Recycle確認を、影響surfaceで実操作する。
- reload persistence、cancel/retry、empty/error、keyboard/focusを含める。
- console error/warning、WPF unhandled error、残留process/portを記録する。

### 7.4 Safety

- user state/cache/sourceのbefore/after hash、または完全非接触を示すoverride path。
- passive workflow前後のEnhancement jobs hashとworker/enqueue count。
- Delete fixtureが専用temp copy、Recycle backendがfakeまたは明示disposableである証拠。
- Favorite/Seen/Recent/ViewerStateのmalformed/lock/crash/merge結果。
- user port 3000 owner before/afterと、検証agentが停止したexact process tree。

### 7.5 Visual

- 同一viewport、同一state、同一fixtureのBrowser/WPF screenshot。
- Landing、Viewer、Folders collapsed、Unseen dots、Settings、Modal、empty/error等の変更対象state。
- crop、overflow、sidebar幅、font、border、focus、small viewport/DPI差の目視記録。
- screenshotとは別にfunctional resultを記録する。

### 7.6 Open boundary

- 実装済み / 未実装 / DEFER / NON-GOAL / CURRENT LIMITATIONを分ける。
- open P0/P1/P2、回避策、次のbounded milestone。
- viewer core、private-local launcher、public packaging/distributionを別々に評価する。

## 8. Commercial readinessとprivate-local境界

このframeworkの`commercial readiness`は、「公開販売した」という意味ではなく、ローカル私用製品でも金を取る水準の設計・正確性・回復性を要求する品質呼称である。

### 8.1 判定level

| Level | 条件 |
| --- | --- |
| **NO-GO** | hard no-go、open P0、データ非破壊2以下、またはrelease gate failureが一つでもある |
| **Development** | no-goは解消したが、いずれかの軸が2以下、open P1、またはaggregate/runtime evidenceが不足 |
| **Private daily-use candidate** | open P0なし。全軸3以上。データ非破壊・操作正確性・失敗回復・Browser意味一致が4以上。exact commitのaggregate/runtime/safety evidenceがgreen |
| **Private-local commercial-ready core** | hard no-go 0、open P0/P1 0、全軸4以上、Overall 4.20以上。Browser/WPF closeout gate、cross-runtime、large-library、fault recovery、launcher provenance、visual/a11y、user data非接触が全てexact commitでgreen。既知P2/DEFERとsupport境界を明記 |
| **Exceptional local quality** | commercial-ready coreを満たし、Overall 4.70以上、重要軸に5点の反復証拠があり、長期stressでも新規P0/P1なし |

P2が残っていても、primary flowの正確性やaccessibilityを損なわず、明示された非blocking polishであればprivate-local commercial-ready coreを妨げない。ただしP2という名前でP1影響を過小評価してはならない。

### 8.2 現在scopeに含めないもの

- installer/self-contained publish。
- code signing、auto-update、rollback channel。
- public security hardening、authentication、CSRF、remote network運用。
- privacy notice、telemetry、account、support SLA、license販売運用。

これらがないことはprivate-local coreの4点を妨げない。一般販売・配布を始める時は、別のdistribution/security/legal reviewを作り、この文書だけでpublic commercial-readyを宣言しない。ユーザーが公開を求めていない間は、remote bind、deployment、telemetry、accountを勝手に追加しない。

## 9. 改善採用フロー

欠陥修正とpower-upは、必ず次の順で行う。

### Step 1 — 再現

- exact commit、surface、契約ID、操作sequenceを記録する。
- temp/isolated fixtureで修正前REDを作る。
- 再現しない提案は欠陥として実装しない。利用価値の提案なら、現行baselineと期待する測定可能な改善を先に定義する。

### Step 2 — Impact

- P0/P1/P2を決める。
- 影響する8軸、source/state/process ownership、Browser/WPF両surface、利用頻度、回避策を記録する。
- hard no-goに触れる場合は他の改善を止める。

### Step 3 — 最小修正

- 変更するstate transitionと所有境界を一つに限定する。
- 大規模rewrite、新palette、新しいshared schema、自動cache削除を同時に混ぜない。
- fallbackは安全側に閉じ、失敗時に既存source/stateを保持する。

### Step 4 — Focused verifier

- 修正前に同じ欠陥を失敗させ、修正後に成功させる。
- happy pathだけでなく、cancel/failure/stale/partial/reload/keyboardのうち影響する境界を含める。
- real user state/cache/source、port 3000、real Explorer/Recycleを必要としないfixtureを優先する。

### Step 5 — Aggregate

- focused verifierの後、影響surfaceのbuild/static/aggregateを直列実行する。
- P0/P1、shared state、Delete、launcher、Browser意味変更はcloseout aggregateとcross-runtimeを省略しない。
- 大量catalog、reload soak、runtime provenance、visual evidenceを変更リスクに比例して追加する。

### Step 6 — Spec update

- 対応するBrowser/WPF要件、parity ledger、source/test map、CURRENT LIMITATIONを同じchangeで更新する。
- 意図した意味変更でない場合、Browser正本を書き換えて欠陥を正当化しない。
- test件数はlive inventoryとして更新し、古い固定値へ合わせない。

### Step 7 — 採用判断

- 変更前後の8軸を再採点する。
- 少なくとも対象軸が改善し、他軸が下がらず、hard no-goが0であることを確認する。
- evidence packと残差を残し、次の最上位候補をSection 10で再計算する。

修正後に別軸が下がった、user data/process ownershipが不明、aggregateが不安定、性能改善がcorrectnessを削った場合は採用せず、変更を縮小または再設計する。

## 10. Next-upgrade優先順位

### 10.1 並べ替え規則

1. hard no-goとP0は式に関係なく最優先。
2. 次にP1、次にP2。同じseverity内でpriority scoreを使う。
3. P0/P1が0件になってから、commercial readinessの最低得点軸を優先する。
4. 同点なら、Browser/WPF両方へ効くもの、利用頻度が高いもの、変更範囲が小さく元へ戻せるものを先にする。

### 10.2 Priority formula

各候補を1〜5で採点する。

- `I` Impact: 失敗/改善が1回起きた時の利用者影響。
- `F` Frequency: 日常利用で起きる頻度。
- `R` Reach: 影響するworkflow/surface/user stateの広さ。
- `C` Confidence: 再現fixtureと原因特定の確かさ。
- `L` Leverage: 一つの修正で複数軸/Browser/WPFへ効く度合い。
- `E` Effort: 実装・test・reviewの工数。
- `K` Change risk: source/state/process ownershipや広いrewriteへ触れる危険。

```text
SeverityBase = P0:3000, P1:2000, P2:1000

Priority = SeverityBase
         + 30*I
         + 20*F
         + 20*R
         + 15*C
         + 15*L
         - 20*E
         - 15*K
```

severity baseの差を、下位severityの高い魅力度で逆転させてはならない。式は同じseverity内の順番を透明にするために使う。

候補には次も併記する。

```text
Candidate:
Contract IDs:
Severity:
Affected axes:
I/F/R/C/L/E/K:
Priority:
Expected measurable outcome:
Required focused verifier:
Aggregate gate:
Rollback/reject trigger:
```

### 10.3 Power-upの方向

最低得点軸が同じ場合、次の順で価値を探す。

1. **安全と正確性**: ownership、guard、atomicity、latest-generation、neighbor/order。
2. **失敗しても続けられること**: partial result、retry、cancel、crash recovery、mixed warning。
3. **大量でも意味を変えない速さ**: full catalog、bounded realization/decode、priority、latency/WS baseline。
4. **操作の短縮**: bulk、keyboard、native FileDrop/Explorer、focusを、既存意味を変えず追加。
5. **理解しやすさ**: empty/error/status、disabled reason、safe diagnostics、accessibility。
6. **native advantage**: BrowserのCURRENT LIMITATIONを複製せず、WPFで安全に超える。
7. **P3製品拡張**: Enhancement ownership、cache retention、packagingは、P0〜P2がgreenで明示的な製品判断がある時だけ着手。

見た目だけの刷新、機能数を増やすための追加、測定できない「高速化」は、上記の候補より先にしない。

## 11. 継続レビューcycle

- **変更ごと**: 影響する軸、hard no-go、focused verifierを確認する。
- **P0/P1修正ごと**: affected aggregateとcross-runtime/safety gateを実行し、全8軸を再採点する。
- **milestone closeout**: exact commitでSection 7のevidence packを作り、commercial readinessを判定する。
- **Browser正本変更時**: WPF impact、parity classification、CURRENT LIMITATIONを同時に見直す。
- **改善3件ごと**: open finding全件のseverityとpriorityを再計算し、古い仮説を惰性で続けない。
- **性能trend**: 同一machine/build/fixtureのbaselineを残し、単発値ではなく複数runで比較する。

各cycleの出力は次の5点に絞る。

1. 現在のscorecardとreadiness level。
2. 新規/解消P0・P1・P2。
3. exact evidenceとuser data/process非干渉。
4. 最も価値が高い次の候補2〜3件。
5. 今回は実装しないDEFER/NON-GOALと理由。

「究極」「完成」「100%」「有償品質」は、該当readiness条件とlive evidenceが揃った範囲だけに使う。reviewは改善を止めるためではなく、最も価値が高い方向へ安全に力を集中し続けるために使う。
