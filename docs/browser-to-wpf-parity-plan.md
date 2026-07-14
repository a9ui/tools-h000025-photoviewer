# Browser to WPF Parity Plan

最終更新: 2026-07-14

browser contract: `docs/browser-feature-contract.md`
WPF implementation: `local-native/PhotoViewer.Wpf/**`

## 1. 目的

WPF を「ブラウザに似た画面」にするのではなく、ユーザーがブラウザで使っている操作と状態の意味を、native runtime で再現する。そのため、browser の現行機能を次の3種類に分ける。

- `REQUIRED`: surface が違っても意味を合わせる。
- `NATIVE_EQUIVALENT`: gesture や layout は WPF に合わせてよいが、結果を合わせる。
- `DEFER / DECIDE`: browser 固有の実装や既知の弱点。コピーせず product decision を先に置く。

この文書の WPF 状態は README の自己申告だけでなく、`MainWindow.xaml` / `MainWindow.xaml.cs` の live code を基準にした。

## 2. 先に固定する non-negotiable contract

1. Favorite の値は共有 path -> `0..5` で、Lv filter は threshold ではなく exact-match の独立 multi-select。
2. level を1つも選ばない Favorites only は `All`。Favorite 値1..5を全て含む。
3. `Unseen dots` は既定 OFF の表示設定。seen state と unseen-only filter を消したり反転しない。
4. seen は explicit selection / preview / modal navigation で付け、単に画面内へ realize しただけでは付けない。
5. source Delete は Recycle Bin。成功後は現 order の隣へ継続し、hard delete fallback を持たない。
6. enhancement は explicit action only。通常 browsing から job/worker を起動しない。
7. 画像数を silent truncate しない。UI virtualization と scan/index limit は別問題として扱う。
8. shared `.cache/favorites.json` と `.cache/recent-folders.json`、WPF側の `.cache/seen.json` を壊さず additive に扱う。browser seen は現状 `pvu_seen_images` で、自動共有しない。

## 3. 現在の browser / WPF 差分

| Area | Browser current contract | WPF current state | Gap / decision | Priority |
| --- | --- | --- | --- | --- |
| Runtime | Next.js local server、port 3000 | .NET 8 WPF、server/Node/WebView不要 | native runtime の差は意図通り | Keep |
| Landing | multi-folder picker、改行 paste、remove、last/recent、shared recent | multi-root、paste、recent/shared recent smoke あり | open/remove/refresh の manual sweep と malformed recovery を追加 | P1 |
| Formats | PNG/JPEG/WebP/AVIF/GIF | PNG/JPEG/WebP/BMP/GIF/TIFF、AVIFなし | AVIF decoder追加か明示非対応。BMP/TIFFは native extension | P1 |
| Scan/index | incremental cache、folder signature、変更差分、silent global capなし | recursive scan、parallel decode/metadata。ただし `MaxLoadedImages = 1200` で `Take(1200)` | 1200件以降が検索も閲覧もできない。最優先で撤去 | P0 |
| Search token | comma区切り AND、prompt + filename | space区切り AND。real tile の initial `Prompt` は path、real PNG prompt は active preview lazy | query grammar と indexed prompt contract が不一致 | P1 |
| Suggestions/chips | prompt tag suggestions、chip add/remove/reorder | plain search input と clear | chip UIをそのまま移す必要はない。comma AND と prompt検索を先に再現 | P2 |
| Quick Search | Portrait/Landscape/Anime/Photoreal | quick search相当が限定的 | preset query文字列を同一契約にする | P2 |
| Favorite values | `0..5`、+1/-1、shared file | `0..5`、+1/-1、shared favorites、import smokeあり | 値と保存はほぼ一致 | Keep |
| Favorite filter | Lv1-5 checkbox独立、exact、empty=All | RadioButton 1つ、`Lv N+` threshold | ユーザーが指摘した主要回帰。意味を置換する | P0 |
| Unrated | level 0 exact、Favoritesと排他 | Unrated onlyあり | interaction/count/persistenceの合同 testを追加 | P1 |
| Seen store | explicit viewでadditive、browser localStorageのみ。`.cache/seen.json` との自動同期なし | shared `.cache/seen.json`、legacy merge、import/reload smokeあり | meaningは合わせる。共有が必要なら明示import/exportを別契約にする | Keep/P1 |
| Unseen filter | browser contract上のseen mapを基準 | Unseen onlyあり | browserに同等UIが弱いので、shared meaningのみ合わせる | Keep |
| Unseen dots | default OFF、visibility only | grid templateは `Tile.Unseen` を常時 dot表示。listはdotなし。設定なし | display setting追加、grid/list両方に適用 | P0 |
| Date filter | created/birth date | `ModifiedUtc` date | 同名UIで意味が違う。created基準へ合わせるかラベルを分ける | P1 |
| Sort | modified new/old、created new/old、name、stable random + reshuffle | 同じsort modeとrandom seed実装あり | README/contractをlive codeへ同期し、fixture test追加 | Keep/P1 |
| Folder buckets | show/hide、multi/range/invert/selected actions | show/hide/show selected/hide selected/clear。multi-rootあり、range selection不足 | range/invertとbucket key across rootsを追加 | P2 |
| Grid/List | virtual page/overscan。双方 large collection対応 | gridは96 batch、384 bounded window。listはfull collectionをItemsSource | list virtualizationと全件collection costを計測 | P0 |
| Display style | Standard/Compact/Poster | 3 style値あり | screenshotで実差とpersistを確認 | P1 |
| Aspect | Original/1:1/2:3 | Original/Square/Portraitとsmokeあり | meaningはほぼ一致 | Keep |
| Thumbnail zoom | 40..600、Ctrl-wheel/+/-/0、center anchor | size control/shortcut/wheelとsmokeあり | center anchorとlarge listで再確認 | P1 |
| Date sections | bounded resultでsection headers | native同等なし | product valueを見て後段 | P3 |
| Multi-selection | Ctrl toggle、Shift range、bulk favorite/delete | ListView multi-selection、selected count、background clearあり | range、bulk favorite、bulk recycleを追加 | P2 |
| Drag out | browser DataTransferでsourceを外へdrag | なし | native drag/drop要否を決める | P3 |
| Right preview | resize 240..900、persist、metadata、bulk actions | selected preview、async decode、splitter、metadata、open/tabあり | bulk actionsと詳細状態を追加 | P2 |
| Preview tabs | open/hover/activate/pin/close/close all/reopen | 同等のUI/hover/pin/reopen/reload smokeあり | current READMEを更新すべきだが、このlaneではlocal-native文書を変更しない | Keep/P1 |
| Pin semantics | browserはpin idのみでreload tab復元が弱い | WPFはpin state/tab persistenceを実装 | browserの弱点をコピーしない。WPFの強いsemanticsを維持 | DECIDE |
| Modal order | current filtered order、端wrap、Delete後neighbor | previous/nextあり。端はclamp、Delete disabled | wrapとneighbor contractが不足 | P0/P2 |
| Modal gestures | edge click、center chrome、double-click metadata、swipe | button/key nav、zoom/pan/flip、metadata sidebarあり | native equivalentを選び、edge/swipeの価値を別判定 | P2 |
| Modal metadata | Prompt/Negative/Settings/PNG Info、copy、prompt tag search | lazy PNG metadata、Prompt/Negative/Settings、copy smokeあり | prompt tag -> search とfallbackを再確認 | P1/P2 |
| Source Delete | guarded Windows Recycle Bin、confirm、neighbor、bulk | Delete button disabled、実装なし | 最重要 missing workflow | P0 |
| Delete safety | project root/index/type guard、hard-deleteなし | なし | disposable copy smokeとguard test必須 | P0 |
| Settings | confirm delete、Unseen dots、edge ratio、editable key bindings | settings shell/read-only keybinding decision。必要behavior setting不足 | Settings foundationをP0 contractの受け皿にする | P0 |
| Enhancement view | explicit enqueue、queue、cancel/retry/open/delete、version toggle | enhanced job file read、Enhanced only、original/enhanced view。enqueueしない | read-only parityは維持。explicit enhancement実装は最後 | P3 |
| Persistence | browser localStorage + shared cache、debounced writes | WPF state/SQLite/shared JSON、SaveState同期箇所あり | schema version、malformed fallback、UI-thread write costを整理 | P1 |
| Errors | 一部inline、一部console/silent | status/MessageBox/一部silent | native向け統一error surfaceを作る | P1 |
| Accessibility | focus-visible/dialog name改善、card semantics未完 | native keyboard/focusは部分的 | tab order、focus return、screen reader nameをmanual sweep | P1 |

## 4. 実装順

### Milestone WPF-P0A: contract sync and settings foundation

Scope:

- Favorite level selector を checkbox 1..5 の独立選択へ変更。
- selected set が空の時は All。
- exact-match filter へ変更。
- favorite filter state を versioned WPF state に保存。
- `Unseen dots` setting を default OFF で追加。
- grid/list の dot visibility を同じ setting に接続。
- seen map と unseen-only filter は変更しない。

Acceptance:

1. fixture に favorite level 0,1,2,3,4,5 を1枚ずつ作る。
2. All は1..5の5枚、Lv1は1枚、Lv1+Lv4は2枚、全level OFFに戻すと5枚。
3. reload後もLv1+Lv4を維持する。
4. Unseen dots default OFFでdot 0、ONでunseen件数と同じdot数、OFFに戻してもseen JSONはbyte-equivalentまたは意味-equivalent。
5. grid/list両方で同じ結果。
6. shared favorites/recentとWPF seenの既存smokeが継続green。browser seenとの暗黙同期は受入条件にしない。

### Milestone WPF-P0B: Recycle Bin and neighbor continuation

Scope:

- single selected source delete。
- confirm/cancel、Do not ask again。
- Windows Recycle Bin only。
- modal/current filtered orderのneighbor選択。
- bulk recycleはsingle flowが安定してから追加。

Acceptance:

1. test専用temp folderのcopyだけを使う。
2. cancelでsource/index/selection不変。
3. 3枚のmiddle削除 -> 次、last削除 -> 前、only削除 -> modal close/empty state。
4. filter subset内で同じneighbor rule。
5. project root、active set外、unsupported typeを拒否。
6. Recycle Bin failureでindexから消さず、hard deleteしない。
7. favorite/seen/recent/enhancement job fileを破損しない。

### Milestone WPF-P0C: remove the 1200-image product cap

Scope:

- scan/index collection と realized UI collectionを分離。
- `Take(1200)`を削除。
- gridのbounded realizationを維持。
- listにもvirtualizationまたはpagingを適用。
- search/filter/modal orderは全indexを対象にする。

Acceptance:

1. 5,000画像fixtureで indexed/visible totalが5,000。
2. 1,201件目以降をfilename searchで選択・preview・modal openできる。
3. grid realized itemはbounded、listのcontainer realizationもbounded。
4. scroll forward/backで欠落・重複・order driftなし。
5. warm run、cold run、selection-to-preview latencyをbefore/afterで記録。
6. cancellationとlatest-selection decode guardを維持。

### Milestone WPF-P1: search/date/state/error alignment

Scope:

- comma AND query grammar。
- filename + indexed PNG prompt search。
- browser Quick Search query contract。
- created/birth date filter、またはUI labelをModifiedと明示するproduct decision。
- state schema version + type normalization + malformed fallback。
- debounced/async state save。
- unified non-destructive error presentation。
- AVIF support decision。

Acceptance:

1. prompt-only token、filename-only token、2-token AND、comma whitespace、no-resultをfixtureで証明。
2. createdとmodifiedが異なるfixtureでdate semanticsを証明。
3. malformed/null/out-of-range stateでstartup crashなし、valid fieldは可能な範囲でpreserve。
4. rapid search/resize/filter操作でUI thread stallを計測。
5. missing permission、decode failure、open failureをsource stateを失わず表示。

### Milestone WPF-P2: interaction completion

Scope候補:

- Shift range / Ctrl toggle と bulk favorite。
- bulk Recycle Bin。
- modal wrap、edge-zone equivalent、immersive chrome、feedback。
- right preview bulk state。
- folder bucket range/invert。
- prompt tag -> search。
- focus return / tab order / accessible naming。

各sliceは1つのworkflowとして閉じ、large fixtureとstate regressionを毎回再実行する。

### Milestone WPF-P3: explicit enhancement

browserのqueueをnativeへ移す前に、次をproduct decisionする。

- WPFがjob enqueue ownerになるか、既存browser queueを監視するだけか。
- ncnn / ComfyUI / local adapterをどこまで直接hostするか。
- output version selectorとsource/original toggleを共有job schemaで扱うか。

Acceptance minimum:

- passive browsingでenqueue/worker startが0。
- explicit action 1回でjob 1件。
- cancel/retry/open/delete outputをsource deleteと分離。
- managed output root外を削除しない。

## 5. Browserの弱点をそのまま移さない

- client filter count mismatch
- pinが完全なreload restoreではない点
- card click-able divの不完全なkeyboard semantics
- localStorageのunversioned `pvu_view`
- scan cancel不在
- errorがconsole/silentに散る点
- multiple enhancement poller

WPFではこれらを「parity」と呼んでコピーしない。必要なuser outcomeを保ちつつ、native implementationで改善する。

## 6. 毎 milestone の共通 gate

1. `local-native/PhotoViewer.Wpf/**` 以外を変更しない。ただしshared contract docを更新するcloseout commitは分離してよい。
2. Debug / Release build 0 error。
3. 対象workflowの専用smoke。
4. existing startup/state/favorites/seen/recent/folder-set/preview/modal/metadata smokesから影響範囲を再実行。
5. disposable fixture以外をDeleteしない。
6. ordinary browsing前後で `.cache/enhance/jobs.json` と isolation counterが不変。
7. screenshotを実寸で確認し、overlap、crop、focus、text fitを記録。
8. user/shared stateを削除しない。migrationはadditiveか明示export/importのみ。
9. GitHub issue/PRとSQLite jobにtest evidenceを短く反映。

## 7. 推奨する次の1件

次は `WPF-P0A: independent Favorite Lv1-5 + Unseen dots visibility + Settings foundation` を1マイルストーンとして実装する。これはユーザーが最初に指摘したbrowser回帰と直接対応し、Deleteより破壊性が低く、その後のDelete確認設定の受け皿も作れる。
