# Browser版 PhotoViewer 正規要件・実装仕様書

文書状態: **Normative / implementation-ready**

最終監査日: 2026-07-18 JST

基準リポジトリ: H000025 PhotoViewer

基準commit: この文書と同じ統合commit（最終closeoutでcommit IDを記録する）

実動作確認: 通常production launcherを隔離portで検証する。ユーザー利用中の `http://localhost:3000/` は停止・再build・再起動しない。
関連追跡: GitHub issue #316 / #318、SQLite improvement item #33

## 0. この文書の目的と読み方

### BR-DOC-001 正本

この文書は、現行Next.jsブラウザ版PhotoViewerを別のAI・開発者が再実装し、同じ意味・操作・安全性を受入試験で確認できる粒度に固定した正規仕様書である。

画面の見た目だけでなく、次を契約対象にする。

- ユーザー操作と状態遷移
- 検索、filter、sort、selectionの意味
- Favorite、Seen、Delete、Enhancementの非回帰条件
- API、データmodel、永続化、cache
- loading、empty、error、responsive、keyboard、accessibility
- performance、cancellation、concurrency、安全境界
- 現行実装の既知制約と、別surfaceへコピーしてはいけない欠陥

### BR-DOC-002 規範語

- **MUST**: 同等製品と認めるため必須。
- **SHOULD**: 原則実装する。省略時は理由と代替受入条件が必要。
- **MAY**: surface固有の裁量。
- **CURRENT LIMITATION**: 現行ブラウザに存在する制約。互換要件ではなく、別実装は改善してよい。
- **NON-GOAL**: 現行製品の対象外。勝手に追加して正規仕様を変えない。

### BR-DOC-003 証拠の優先順位

仕様が衝突した場合は、次の順で判断する。

1. この文書のMUSTと明示された安全契約
2. 基準commitの実コードとroute
3. 基準commitのtest
4. port 3000の実動作
5. 過去README、古いrecap、旧WPF資料

現在のユーザーlocalStorageやcacheに入っている値は「ユーザーの保存値」であり、既定値の証拠にはしない。

### BR-DOC-004 WPFへの適用

WPFはDOM、CSS、HTTP APIをそのまま複製する必要はない。ただし、要件IDの結果、状態の意味、安全境界、受入条件は合わせる。Native向けの変更可否は [browser-to-wpf-parity-plan.md](./browser-to-wpf-parity-plan.md) で管理する。

## 1. 製品定義

### BR-PROD-001 目的

PhotoViewerは、Windows上のローカル画像フォルダをindex化し、大量の生成画像を高速に検索・比較・評価・整理するlocal-first viewerである。Stable Diffusion PNG metadataを利用できるが、画像生成器やupscalerそのものではない。

### BR-PROD-002 最重要の非回帰条件

次はMUST。

1. source画像のDelete成功後、同じ閲覧順の隣の画像へ継続する。
2. Favorite Lv1〜5は独立してON/OFFできる。level未選択時はFavorite全体を表す `All` になる。
3. Seenは明示閲覧時だけ加算し、`Unseen dots` はSeen状態を変えず表示だけをON/OFFする。既定はOFF。
4. galleryのzoomでsidebarや文字サイズを変えない。
5. galleryのzoom前後で、見ていた基準画像のviewport内位置を極力保つ。
6. 通常のscan、一覧表示、preview、modal navigationはEnhancement jobをenqueueせず、workerも開始しない。
7. source DeleteはWindows Recycle Binだけを使い、hard-deleteへfallbackしない。

### BR-PROD-003 明示的に存在しないUI

次は現行Browser UIに存在せず、再実装で復活させない。

- `Quick Search`
- `Today`
- `7d`
- `30d`
- `This year`

`powershell -File scripts/verify-ui-regression-guard.ps1`はBrowser/WPF live UI sourceにこれらのcontrolとFavorite threshold labelが再侵入していないことを固定する。

Date filterは手動のFrom/Toだけを持つ。Folders sectionは削除せず、既定展開かつ折り畳み可能にする。

### BR-PROD-004 NON-GOALS

- cloud sync、account、multi-user collaboration
- source画像の編集・上書き
- permanent delete
- editable user tag database
- 閲覧を契機にした自動Enhancement
- remote公開を前提としたweb service
- deployment。H projectのVercel配備は別途明示承認が必要

## 2. Runtimeとtrust boundary

### BR-RUN-001 Stack

- Next.js 16.2.10
- React 19.2.7
- Node.js 20.9以上
- pnpm 11以上
- TypeScript
- Windows local machine

### BR-RUN-002 Production launcher

通常launcherは `scripts/prod_launcher.js`。

1. `--help` / `-h` はusageだけを出し、process cleanup、port probe、build、server、ComfyUI、browserを一切開始せず0で終了する。
2. 引数なしでは、Windows上で同じproject root配下のlauncher/wrapper/Next executableをcommand lineの完全path tokenで確認できる古いproject server processだけを安全に整理し、port 3000から3999までの最初の空portを選ぶ。path比較はWindows上でcase-insensitiveだが、prefix一致や単なる引数中のroot文字列は所有根拠にしない。
3. `--port <1..65535>` / `--port=<1..65535>` は指定した1 portだけをprobeする。invalid/busyならstale process cleanupや別port fallbackをせず明示errorで終了する。
4. `.next/BUILD_ID` がない、または監視対象source/configがbuildより新しければproduction buildする。
5. `next start --hostname 127.0.0.1 --port <port>` をparent-watch wrapperで起動する。launcher消失時はwrapperが直接生成したserver process treeだけを停止し、同じportを後から取得した別processを停止しない。
6. ready後にbrowserを開く。

引数なしの3000は既定候補であり、競合時に必ず3000へ固定する契約ではない。空port判定はIPv4/IPv6の片方だけをprobeせず、どちらかに既存listenerがあれば次portへ進む。明示portは常に`127.0.0.1`へbindし、指定portを使用中のprocessをlauncher都合で停止しない。`PVU_NO_OPEN=1` は検証時にbrowser自動openだけを抑止できる。

launcherはbuild後にsource revision、dirty state、build ID、build完了時刻、host、port、launcher PIDを1行のprovenance logへ記録し、同じ値をserver processへ渡す。`GET /api/runtime` はabsolute project pathを返さず、served processのbuild/source identityをno-store JSONとして返す。

### BR-RUN-003 Enhancement autostart

ComfyUI autostartは既定OFF。`PVU_COMFY_AUTOSTART=1` の明示時だけlauncherが起動経路を使える。

### BR-RUN-004 Local trust boundary

現行APIにはauthentication、CSRF token、Origin validationがない。したがってMUST:

- Browser版は信頼済みlocal machine用として扱う。
- 外部networkへ公開しない。
- 別実装はこの無認証HTTP境界を互換要件としてコピーしない。
- absolute pathを扱うため、log・diagnostic・screenshotの共有時はprivacyに注意する。

通常production launcherはIPv4 loopback `127.0.0.1`へ固定する。Firewall等の外側条件をこの仕様の安全証明にしない。実listener、runtime provenance、非loopback interfaceからの接続拒否を `scripts/verify-browser-runtime.ps1` で検証する。

## 3. 主要データmodel

### BR-DATA-001 Image identity

画像のidentityは正規化したabsolute path。`ImageFile.id` と `ImageFile.absolutePath` はこのidentityを使う。

```ts
type ImageFile = {
  id: string;            // normalized absolute path
  filename: string;
  absolutePath: string;
  fileUrl: string;       // /api/image?path=...&thumb=true
  displayUrl: string;    // display-size route
  fullUrl: string;       // original route
  metadata: SDMetadata | null;
  createdAt: number;
  mtime: number;
  isFavorite?: boolean;  // legacy/derived compatibility field
};
```

同じabsolute pathが複数rootに含まれても1画像として扱う。

### BR-DATA-002 Supported formats

拡張子はcase-insensitive。

- PNG
- JPG / JPEG
- WebP
- AVIF
- GIF

### BR-DATA-003 PNG metadata

```ts
type SDMetadata = {
  raw?: string;
  prompt: string;
  negativePrompt: string;
  settings: Record<string, string>;
};
```

- PNG signatureを確認し、最初のIDATより前の `tEXt` chunkを読む。
- keywordが厳密に `parameters` の最初の値をStable Diffusion形式としてparseする。
- Prompt、Negative prompt、Steps以降のkey/valueを分離する。
- parse不能時は画像自体を失敗させずmetadataをnullにする。
- index保存時は `raw` を除外する。
- filenameとpromptだけが検索対象。negative prompt、settings、absolute pathは検索対象外。

### BR-DATA-004 Time semantics

- Modified sortはfilesystem mtime。
- Created sort/date filterは `birthtime || ctime || mtime`。
- filesystemによりcreatedの意味が異なり得る。別実装は採用したfallback順を明示する。

### BR-DATA-005 Favorite

Favoriteは `absolutePath -> integer level`。

- 0: unfavorite。保存mapから除去してよい
- 1〜5: exact level
- 旧boolean/truthy値を読む場合は1へnormalize
- 数値は1〜5へclamp

### BR-DATA-006 Seen

Seenは `absolutePath -> true` のadditive map。画像を明示的に閲覧した事実を表す。viewportに描画された事実やdot visibilityとは別。

### BR-DATA-007 Folder set

- 1行1absolute pathの集合。
- 前後空白とblank行を除く。
- case-insensitiveに重複排除し、最初のspellingを保持する。
- folder set内の順番は意味を持つ。

### BR-DATA-008 Viewer index session

- scan `complete`はcanonical folder setから導出したopaque `indexToken`を返す。形式は`idx_` prefixだが、clientは内容を解釈しない。
- serverはtokenごとにimage list、sort/search/tag/folder derived cacheを分離する。同じprocessの別Browser windowが別folder setをscanしても相互の結果を上書きしない。
- token snapshotは最大8件、last-useから30分TTL。LRU/TTLで失効し得る。
- token付きsearchが失効した場合は410と再scan案内を返す。clientは既存resultを勝手に別sessionのfallback indexへ切り替えない。
- tokenなしrequestは互換用のactive fallback indexを使う。新しいclientはscan完了後の全viewer requestへtokenを伝播する。
- tokenはauthentication secretではない。loopback/local trust boundaryを置き換えない。

## 4. 画面状態と遷移

### BR-STATE-001 Top-level phase

Top-level phaseは次の3つ。

| Phase | Entry | Exit |
| --- | --- | --- |
| Landing | 起動、logoから戻る、scan failure | folder setをscan |
| Scanning | Open/Refresh | completeでViewer、fatal errorまたは明示CancelでLanding |
| Viewer | scan complete | logoでLanding、別scanでScanning |

### BR-STATE-002 Overlay/surface

Viewer上には独立して次が重なる。

- mobile sidebar overlay
- right preview panel
- bottom preview tabs
- image modal
- settings dialog
- delete confirmation alertdialog
- enhancement queue

Modalが開いている時はgalleryのglobal Delete/Favorite/arrow shortcutを発火させない。

### BR-STATE-003 Viewer layout

Desktopの概念配置:

```text
+-------------------------------------------------------------+
| Header: menu / logo / refresh / search / count / panels     |
+------------+-------------------------------+----------------+
| Sidebar    | Virtualized Gallery            | Right Preview |
| 240px      | flex                           | default 320px  |
| fixed      |                                | 240..900px     |
+------------+-------------------------------+----------------+
| Bottom Preview Tabs, when at least one tab exists            |
+-------------------------------------------------------------+
```

Sidebarはdesktopでwidth/min/max/flex-basis 240px。gallery zoomでこの幅を変えない。

### BR-STATE-004 Visual tokens

別実装は現行design languageを基準にし、独自palette/radius/type scaleを発明しない。

| Token | Value |
| --- | --- |
| primary / secondary / tertiary / elevated background | `#08080c` / `#101018` / `#181824` / `#1c1c2e` |
| primary / secondary / tertiary text | `#eef0f6` / `#a0a6bf` / `#81869f` |
| accent / accent light | `#6366f1` / `#818cf8` |
| favorite / danger / success | `#f43f5e` / `#ef4444` / `#10b981` |
| radii | 8 / 12 / 16 / 24 / full pill |
| body font | system UI → Segoe UI fallback |
| focus | 2px accent-light、2px offset |
| scrollbar | 8px、transparent track、10% white thumb |

Glass surfaceはsecondary backgroundの75% alpha、16px blur、6% white borderを基準にする。UI changeは同一viewport/stateのreferenceと新screenshotを並べ、crop、padding、font weight、border、radius、overlapを確認する。screenshotだけをfunctional QAの代用にしない。

## 5. Landingとfolder memory

### BR-LND-001 Visible controls

Landingは次を持つ。

- product title `PhotoViewer`
- subtitle `Index and search Stable Diffusion PNG metadata locally`
- `Add folder`
- folder setが空ならdisabledの `Open folder set`
- 選択済みfolder rowとremove
- `Paste one absolute path per line...`
- `Add pasted`
- `Open last folder set`
- `RECENT FOLDER SETS`
- scan progress
- scan中だけ表示する `Cancel scan`

### BR-LND-002 Add/paste/remove

- `Add folder` はWindows pickerで選んだpathを現在setへ追加する。
- PickerはWindows COM dialogを優先し、利用不能時はfallback dialog経路を試す。
- paste欄は1行1path。`Ctrl+Enter` / `Cmd+Enter` でも `Add pasted` と同じ。
- add時にtrim、blank除去、case-insensitive dedupeする。
- paste追加だけではscanを開始しない。
- rowのremoveはそのfolderだけを現在setから外す。

### BR-LND-003 Last/recent

Shared schema:

```ts
type RecentFoldersState = {
  version: 1;
  lastFolderSet: string[];
  recentFolderSets: string[][];
  updatedAtUtc: string;
};
```

- recentは最大12 set。共有writeでは各writerの最新setをadditive historyへ残し、単一のlastFolderSetは最後に成功したlock holderを採用する。
- set比較はcase-insensitive。
- last setをrecentの先頭へ置く。
- browser local folder memoryが空の時だけshared stateを初期importする。
- active `dirPath`変更時にbrowser-local last setを書き、scan completeでrecentとshared stateを更新する。
- malformed shared stateは勝手に上書きせず、PUTは409で保護する。
- legacy stateはadditive importとし、現行stateを消さない。

shared、browser-local、legacy additive importのset dedupeはすべてcase-insensitiveとし、最新setのspellingとfolder順を保持する。

### BR-LND-004 Scan start

- 通常click: incremental scan。
- Shift-click: force full verification。
- folder setが空なら開始しない。
- scan中はbuttonをScanning状態にする。
- scan中のprogress surfaceは`Cancel scan`を表示する。Cancel後は同じfolder setを編集または再scanできるLandingへ即戻る。

### BR-LND-005 Scan時のview reset

新しいfolder setのscan開始時はDate From/Toとhidden folder setをresetする。Favorite値、Seen map、shared recent/cacheを削除するresetではない。

## 6. Scan、index、cache

### BR-SCAN-001 Traversal

- 各rootを `path.resolve` する。
- root直下の対応画像を非再帰targetとして扱う。
- root直下の各folderを個別targetとして再帰scanする。
- symbolic linkはfollowしない。
- 複数rootは順番に処理する。
- absolute pathでdedupeする。

### BR-SCAN-002 Incremental behavior

Cache:

- `.cache/index_<md5(lowercase resolved root)>.json`
- `.cache/folders_<same hash>.json`

Index schemaはversion 1、folder signatureはversion 2。

- mtimeとsizeが同じ既存fileはmetadataを再利用する。
- changed/new fileだけmetadataをparseする。
- stale fileを除去する。
- unchanged folder signature、cached filesあり、最近activeでないtargetはfast pathで再利用できる。
- 48時間以内にactiveだったtargetは保守的に再確認する。
- target scan failure時は既存cached pathsを維持する。
- force fullはsame-name overwrite等の回復経路。

### BR-SCAN-003 Failure semantics

- 一部root/target失敗: 可能なrootを継続し、partial completeを許す。
- scan全体の結果が0画像かつ1つ以上のroot failureがある: SSE error。正常だが空のrootと失敗rootが混在する場合もerror。
- 全rootが正常に完了し0画像: complete。Viewerのno-supported-images empty stateへ進める。
- `Cancel scan`、client connection切断、unmount/navigation cleanupは現在runのEventSource/requestをcancelする。cancel済みrunはpartial cache、process-global index、client `indexToken`、recent completionをpublishしない。
- 同じcanonical folder setの同時scanは1runだけを許可する。後発runはSSE `error` で競合理由を返し、既存runを壊さない。
- 異なるcanonical folder setは互いを強制cancelしない。
- clientはscan generationでstale `progress` / `complete` / `error` を捨てる。古いstreamが新しい画面stateを上書きしてはならない。
- malformed event、stream切断、same-set conflictはすべてrecoverable scan errorであり、既存Favorite/Seen/shared cacheを削除しない。
- client cancelはrun generationを先に失効させ、progress/errorをclearし、folder setと既存recent/state/cacheを保持する。Cancel後は非blocking statusで完了を知らせ、primary `Open folder set`へkeyboard focusを戻す。cancel後の遅延`progress` / `complete` / `error` / connection errorは全て無視し、二重Cancelはno-op、直後の再scanを許す。

### BR-SCAN-004 SSE progress

`GET /api/scan` はSSEで `progress`、`complete`、`error` を送る。

Payload:

```ts
type ScanProgress = {
  type: "progress" | "complete" | "error";
  processed: number;
  total: number;
  newFiles: number;
  stage?: "preparing" | "scanning" | "complete";
  message?: string;
  indexToken?: string; // complete only
};
```

- keepalive 10秒
- progressはstageごと最大150 eventを目安にcoalesce
- 最小step 25
- 400ms以上silentにしない
- first/final eventを保持
- single-root progressのprocessed/totalはfile counts。
- multi-root進行中はaggregate percentageとしてprocessed 0〜99、total 100を使う。
- multi-root completeはactual image countをprocessed/totalに置き、`newFiles: 0`。
- completeはそのfolder setの`indexToken`を含む。clientはViewer phaseへ入る前に保存し、以後のsearch/folders/tags/image/open/delete/warmup/enhance source validationへ付ける。

UIはfolder progress、処理数、全体percentと`Cancel scan`を表示する。CancelはEventSourceをcloseしてLandingへ即戻り、folder add/remove/pasteを再度有効にする。connection lost、malformed event、same-set conflictはblocking alertではなくLanding内のinline errorへ遷移し、原因、Retry、Dismissを表示する。Retryは直前のfolder setを再利用し、Dismissは入力済みfolder setを保持する。

### BR-SCAN-005 Thumbnail/display cache

- thumb: width 300、WebP quality 72、effort 2
- display: fit-inside最大2200、no-enlarge、WebP quality 86、effort 2
- key: resolved path + mtime + variant
- temp fileからrenameし、破損RIFF/WEBPを検証する
- same output generationをprocess内でcoalesceする
- visible、nearby、backgroundでpriorityを分ける
- derived生成失敗時、表示routeはoriginalへfallbackできる
- Sharp internal cacheはfiles 100、memory 256MB。
- Disk上のthumb/display cacheには現在quota/LRU evictionがない。
- Tempからreplaceするが、Windows retry経路は既存finalをremoveしてからrenameするため、厳密に無欠落のatomic replaceではない。

Source画像が正本。thumb/display cacheは削除・再生成可能。

## 7. Viewer headerとsidebar

### BR-SHL-001 Header

左から概ね次を持つ。

1. sidebar show/hide
2. PhotoViewer logo。clickでLandingへ戻る
3. active folder set refresh。Shift-clickでfull scan
4. SearchBar
5. indexed/filtered count
6. right preview show/hide
7. enhancement queue show/hide

### BR-SHL-002 Count

- `shown` はFavorite、Unrated、Enhancedを含むcurrent client filterを通過し、現在load済みの結果数。
- `loaded` はserver result pageから現在clientへload済みのnon-null件数。
- `indexed` はserver query/filter後の全result total。全library totalと一致するとは限らない。
- partial page loading中は `shown N of M loaded · T indexed` の意味を区別して表示する。client filterがない時は冗長なshown/loadedを省略してよい。
- virtualized DOM/card数を製品件数として表示してはならない。

### BR-SID-001 Folders section

- 見出し `Folders` とfolder countを表示する。
- 既定は展開。
- 見出しbuttonで折り畳み可能。
- collapse状態は`pvu_view.foldersExpanded`へ保存し、reload後も復元する。旧stateまたはinvalid valueは展開へnormalizeする。
- `Quick Search` を置かない。

### BR-SID-002 Active folder controls

- active folder set summary
- `Add folder`: 現在setへappendし、直ちにscan
- `Change folder`: pickerで即replaceせず、現在setを保持したままLandingへ戻す。Landing上でfolderのadd/remove/pasteを編集してからOpenする。
- AddとChangeを同じhandler/意味にしない。

### BR-SID-003 Folder buckets

Bucket key:

- root直下: `__ROOT__`
- nested: 最初のpath segment
- multi-root: root indexを含めて衝突を避ける

操作:

- plain click: selection replace
- Ctrl/Cmd-click: toggle
- Shift-click: anchor range
- Ctrl/Cmd+Shift-click: additive range
- Show all
- Hide all
- Invert
- Show selected
- Hide selected
- Clear selection

`Show selected` は選択bucketをunhideする操作で、選択だけを孤立表示するfilterではない。`Hide selected` はhidden setへ追加する。

Folder listのsortは次を持つ。

- `A-Z`: label昇順。既定
- `Z-A`: label降順
- `Count`: image count降順、同数はlabelの`localeCompare`

### BR-SID-004 Filter controls

- Favorites
- Favorite `All`
- Lv1 / Lv2 / Lv3 / Lv4 / Lv5 独立toggle
- Unrated
- Enhanced
- Date From
- Date To
- active dateのClear

`Today`、`7d`、`30d`、`This year` は置かない。

### BR-SID-005 Sort/display controls

Sort:

- Modified: New
- Modified: Old
- Created: New
- Created: Old
- Name
- Random
- Reshuffle

View:

- Grid / List
- Standard / Compact / Poster
- Original / 1:1 / 2:3
- Size 40〜600、step 20
- performance overlay ON/OFF
- Settings

Style shortcut:

- Compact: Grid + Square + 140
- Poster: Grid + Portrait + 240
- Standard: 他の現在設定を維持

## 8. Search、filter、sort

### BR-SRC-001 Search query

- queryはcomma区切りtoken。
- tokenをtrimし、空tokenを除く。
- case-insensitive。
- token間はAND。
- 各tokenは `filename + prompt` の連結haystackへsubstring一致。
- absolute path、negative prompt、settingsは検索しない。

例:

| Query | Meaning |
| --- | --- |
| `cat` | filenameまたはpromptにcat |
| `cat, night` | catとnightの両方 |
| `night cat` | 1 tokenとして空白込みsubstring |

### BR-SRC-002 SearchBar interaction

- committed chipと、末尾の未確定textを持つ。
- Enterまたはcommaで現在tokenをchip化。
- 空inputでBackspaceすると最後のchipを除去。
- chipのremoveを持つ。
- Desktop mouseはchip全体のHTML5 drag、touch/penはchip左端の明示handleをPointer Eventsでdragし、同じreorder結果へ統合する。
- touch/penは8px未満をtapとして扱い、pointer capture中に8px以上移動した時だけdragを開始する。Drop先はchip中心への2次元距離で決めるため、横並びとmobile wrap/縦配置を同じ規則で扱う。
- reorder成功後は移動したchipへfocusを維持し、positionと総数を`aria-live`で通知する。Pointer cancel、capture loss、drag中の外部query変更、stale/out-of-range indexは並びを変更せずcancelする。
- `touch-action: none`は小さいdrag handleだけに限定し、chip本体と周辺の縦page scrollを奪わない。Reduced motionではdrag transition/scaleを無効にする。
- Keyboardはchip focus中のAlt+Shift+Left/Rightで同じreorder、Delete/Backspaceでremove。Mouse/keyboardの既存操作は維持する。
- chipはfocusable list itemで`aria-posinset`/`aria-setsize`と操作説明を持つ。keyboard reorder/remove後は移動したchip、次、前、最後はinputの順でfocusを回復し、結果を`aria-live`で通知する。Remove buttonのEnter/Spaceはchip shortcutへ二重伝播しない。
- 外部からduplicate tokenを含むqueryが来ても、occurrence identityでfocus/reorder/removeを分離する。
- exact-case duplicateを作らない。
- clearで全queryを空にする。

### BR-SRC-003 Suggestions

- tags endpointを初回fetch。
- Tag集計はPNG promptをcomma分割し、wrapperをtrimしてlowercase化する。
- 2文字未満とliteral `BREAK` を除外する。
- 出現count降順を基礎順にし、APIは最大2000件を返す。
- current inputをsubstringで含むtagを候補にする。
- current chip/exact current valueを除外。
- prefix matchを先、次にcount降順。
- 最大12件。
- ArrowDown/ArrowUp、Enter/Tab、Escape、mouseで操作。
- outside clickで閉じる。

### BR-SRC-004 Debounce/stale request

- SearchBarからcontext反映: 約200ms。
- search fetch: 約150ms debounce。
- 新queryは前requestをAbortControllerでabort。
- generation IDでstale responseを捨てる。
- query debounce待ち中にsort/date/hidden folder/folder set/index sessionが変わった場合は旧timerをcancelし、captured済みの旧条件をあとから復活させない。

### BR-FAV-001 Mutation

- `+` / F: 1段階上げ、最大5。
- `-` / U: 1段階下げ、0でmapから除去。
- toggle互換操作は0と1を往復。
- cardのfavorite controlはselection/Seenを副作用にしない。

### BR-FAV-002 Filter

- Favorites OFF: level selectionは結果へ影響しない。
- Favorites ON + level未選択: `All`。level 1〜5を全て表示。
- Favorites ON + level選択: exact levelのOR。
- Lv1〜5は独立ON/OFF。複数同時選択可。
- Unratedはlevel 0 exact。
- FavoritesとUnratedは相互排他。
- level setはreload後も維持。
- activeなexact Favorite/Unrated filter中にlocal favorite levelを変更してcurrent imageが非該当になった時は、変更前のfiltered orderでnext、なければpreviousへ即時移動する。survivor 0件ならselection/modalをclearする。
- current imageがopen preview tab由来なら移動先をopen tabへ追加してactiveにする。selection/right previewだけから始まった時はtabを捏造しない。
- matching mutation、Favorite filter OFF、shared hydrationだけではcurrent imageを移動しない。

「Lv N以上」のthreshold filterは禁止。

### BR-FAV-003 Merge

- Browser localとshared server mapは通常max levelでmerge。
- 現sessionで変更したdirty pathはlocal exact値を優先し、遅いserver GETで削除や減算を復活させない。
- local primary keyが存在しない時だけbackupを回復元として使う。primaryが空、malformed、または意図的に0件ならbackupを勝手に復活させない。
- shared PUTはclientが読んだ`baseFavorites`と変更後`favorites`を送り、serverはprocess間lock内でlatest disk mapへ変更keyだけをthree-way mergeする。他processが変更した無関係pathを消さない。
- malformed shared file、lock timeout、書込み失敗では既存fileを保持し、clientのlocal exact値も失わない。成功responseのmerged mapを次回baseにする。

### BR-SEEN-001 Seen event

次でSeenへ追加する。

- card/rowを明示click
- keyboard selection move
- modal open
- modal navigation

次では追加しない。

- viewport内へ入っただけ
- thumbnail warmup
- scan
- search result fetch
- favorite buttonだけのclick

### BR-SEEN-002 Unseen dots

- 既定OFF。
- ONかつSeen mapにpathがない時だけGrid/Listへdotを表示。
- OFFはdotを隠すだけ。
- ON/OFFでSeen mapを削除・反転しない。
- 現行BrowserにはSeen全消去UIもUnseen-only filterもない。

### BR-SEEN-003 Browser / WPF shared Seen

- Browser localStorageの`pvu_seen_images`とshared `.cache/seen.json`をadditive unionする。
- 起動時はlocalとsharedのtrue-marker mapをunionし、localにしかないmarkerもsharedへ追記する。
- 閲覧後はlocalStorageを先にdebounce保存し、`PUT /api/seen`でsharedへunionする。
- shared writeはprocess間lock、latest-disk再読込、temp+renameを使う。同時Browser/WPF writeでtrue markerを失わない。
- false、削除、全置換はAPI契約に含めない。Seenはこの経路では単調増加する。
- malformed/unsupported shared documentは409で上書きを拒否し、元fileとBrowser local mapを保持する。

### BR-DATE-001 Date

- manual From/Toのみ。
- Created timeを使う。
- Fromはlocal day startを含む。
- Toはlocal day end 23:59:59.999を含む。
- Clearで両方解除。

### BR-FLD-001 Server folder filter

Hidden folder bucketsをserver searchへ渡し、active dir set内で除外する。malformed `hiddenFolders` queryは空として扱う。

### BR-ENHF-001 Enhanced-only

Succeeded enhancement jobにsourceIdがある画像だけを表示する。Client-only filterで、job状態をpollして更新する。

### BR-SORT-001 Sort order

- newest: mtime降順
- oldest: mtime昇順
- created-newest: createdAt降順、mtime/name tie-break
- created-oldest: createdAt昇順、mtime/name tie-break
- name: case-insensitive/base locale
- random: seed + idから決定的keyを作り、同seedでは安定
- Reshuffle: seedを更新

Unknown sortはnewestへfallback。

### BR-SORT-002 Pagination

- Context page sizeは100。
- API sizeは1〜200へclamp。
- pageは0以上。
- `totalPages` はtotal 0でも最低1。
- Client filterでviewportが埋まらなければ追加pageを漸進load。

## 9. Gallery

### BR-GAL-001 Virtualization

- Grid/Listとも全DOM描画しない。
- page 100、overscan 4 rowsを基準。
- 未fetch slotはplaceholderでscroll geometryを保持。
- visible/nearby画像を優先warm。
- 次pageをbackground fetch。

### BR-GAL-002 Grid sizing

- desired thumbnail sizeと利用可能幅からauto columnsを決める。
- cellは残幅配分でdesired sizeより大きくなる場合がある。
- fixed column preferenceは現行では使わず、旧 `columns` は0/autoへmigration。

### BR-GAL-003 Aspect/style

- Original: 2:3 cell内でobject-containとbackgroundを使う。masonry/natural-height tileではない。
- 1:1: square、object-cover。
- 2:3: portrait、object-cover。
- Compactでcell widthが小さい場合はfavorite badgeを表示するが、card内の+/- controlを省略できる。
- List rowは約86px、thumbnail約56px、filename/path/prompt先頭150文字。

### BR-GAL-004 Date sections

Created sort時だけdate sectionを使う。

- fully loadedかつ結果2500以下: 38px section headerをvirtual layoutへ含める。
- それ以外: row/card separatorへfallback。

### BR-GAL-005 Scroll memory

次を含むview keyごとにscrollを保存する。

- directory set
- query
- sort/random seed
- date
- view mode/style
- Favorite/Unrated/levels/Enhanced
- hidden folders

Thumbnail sizeとaspect modeはkeyへ含めない。memoryは最大80 entry。

### BR-GAL-006 Gallery zoom

Grid:

- SidebarのSize slider
- Ctrl/Cmd + wheel
- range 40〜600
- default 200
- modified wheelはGrid内だけgallery zoomとして捕捉する。画像上ではcursor直下の画像、余白ではviewport center近傍の画像をanchorにする。

List:

- Grid thumbnail settingでList thumbnailを拡大縮小しない。

Browser native page zoom:

- Ctrl/Cmd + `+` / `-` / `0` は捕捉しない。
- Grid外またはList上のCtrl/Cmd + wheelは捕捉しない。

Sidebar、header、text、right panelのCSS sizeはgallery zoomで変えない。

### BR-GAL-007 Zoom anchor

Zoom開始時、viewportのhorizontal centerに最も近い表示画像とそのviewport offsetをanchorにする。reflow後、同じ画像がほぼ同じoffsetへ来るようscrollを補正し、scroll boundsへclampする。

受入許容:

- anchor画像が残る場合、reflow後もviewport内に見える。
- 通常fixtureではoffset driftを1 card未満に抑える。
- sort/filterが同時に変わった場合はanchor保証対象外。

### BR-GAL-008 Selection and keyboard

- click: single select、Seen、right preview。
- Ctrl/Cmd-click: toggle。
- Shift-click: anchor range。
- Ctrl/Cmd+Shift-click: additive range。
- Arrow Left/Right: Grid ±1。
- Arrow Up/Down: Grid ±current columns。
- List arrows: ±1。
- selectionなしでRight/Down: first。
- selectionなしでLeft/Up: last。
- edgeではclamp。Gallery arrowはwrapしない。
- keyboard移動もSeenにし、約35%位置へreveal。
- Grid/List containerまたはvirtual canvasの本当の空白をclickするとselectionをclearする。Card、button、placeholder、virtualized itemへのclickはbackground clearとして扱わない。

CURRENT LIMITATION: range selectionとkeyboard orderは`searchResults.filter(non-null)`のloaded ID subsetを使う。Sparseな大規模resultの未load区間をまたぐfull-order range/navigationは保証しない。別実装は全query order modelを使って改善してよい。

Plain wheelとAlt+wheelはnative scrollへ渡し、Gallery zoomを発火しない。Browser native zoomとの競合を避けるため、window capture handlerが処理するのはCtrl/Cmd+wheelだけである。

### BR-GAL-009 Open behavior

- single click: bottom tabを作らずright preview。
- double click: Seen、pinned bottom tab作成、full modal open。

### BR-GAL-010 Drag out

Drag data:

- `text/uri-list`: full URL
- `text/plain`: absolute path
- `DownloadURL`
- effect: copy

### BR-GAL-011 Empty state

少なくとも次を区別する。

- queryに一致しない
- active client filterに一致しない
- supported imageがない

## 10. Right previewとbottom tabs

### BR-RPV-001 Panel

- view settingでshow/hide。
- default width 320。
- mouse resize 240〜900。
- mouseupでwidthを保存。
- narrow viewportでは非表示。
- active image/selectionがない時は操作案内を表示。

### BR-RPV-002 Single image

- high-priority display image。
- filename/path。
- favorite +1/-1。
- Open external。
- Enhance。
- Show Details。
- active preview tabがcurrent loaded result外なら、`Outside current search/filter`とmodal navigation不可の理由を`aria-live` statusで示す。画像、Favorite、Open、Enhance、Detailsは利用可能なままにする。

Detailsは現行では次だけ。

- Path
- Created
- Modified
- Prompt
- Negative

Dimensions、file size、Open Tab buttonは現行契約に含めない。

### BR-RPV-003 Multi-selection

- selected count
- current resultにまだ存在するselected pathだけをFavorite対象にし、stale/current-result外件数は`unavailable`として明示する。
- Favorite Lv0/Clear、Lv1〜5 exact、全件+1/-1を持つ。
- 対象levelがuniformなら`LvN for N selected`、混在ならlevel集合を含むMixed statusを`aria-live`で示す。
- 1操作はdedupe済み対象を1回のstate transactionで更新し、既存300ms debounce/3-way shared mergeへ渡す。
- Open externalを全件
- Enhance selectedを逐次
- Recycle selectedを逐次
- success/failure message
- Recycle successだけselectionから除外し、failureは選択したままretry可能

### BR-TAB-001 Creation/activation

- Gallery double clickでだけ自動作成し、自動pin。
- single clickは作らない。
- tab clickはactive IDを同期する。Current loaded `searchResults` にtab IDがある場合はselected indexも同期してfull modalを開く。Query/filter変更後などcurrent results外のtabはactive right previewだけを更新し、panel内statusで理由を示す。
- open tabまたはclosed historyがある時だけbottom barを表示する。open 0 / closed historyありではcompact Restore-only surfaceにする。

### BR-TAB-002 Operations

- x close
- middle-click/pointer middle close
- hover 220x220 preview + filename/path
- pin marker toggle
- Restore closed
- Ctrl/Cmd+Shift+T
- closed stack最大30
- pointer drag/dropで左右のdrop positionへreorder
- Alt+Shift+Left/Rightでfocused tabを1位置reorder

最後のtabを閉じた直後はcompact Restore buttonへfocusする。Restore後は復元tabへfocusする。closed historyが0ならbarとRestore actionを表示しない。Ctrl/Cmd+Shift+Tはbarの有無に関係なく使える。

Active tabを閉じた時は残りの末尾をactiveにする。

Reorderはactive/pin/image dataを変えず、tab orderだけを変更する。unknown ID、duplicate state、範囲外/非整数destination、同位置はstable no-op。tabには`aria-posinset`/`aria-setsize`とkeyboard chordを公開し、drag中はdrop位置を視覚表示する。

### BR-TAB-003 Reload persistenceとPin

- open tab orderとactive IDは`pvu_preview_tabs`へversion付きで保存し、最大30件を復元する。
- reorder後のorderも同じsnapshotへ保存し、reload後に維持する。
- reload後の復元はcurrent indexed/search resultに存在するsupported absolute pathだけを採用する。obsolete、duplicate、relative、unsupported、malformed snapshotは安全に無視する。
- active IDが復元集合外なら先頭tabへfallbackする。
- tab集合の復元だけでmodalは開かない。active tabはright previewとselectionを同期する。
- Pinは`pvu_pinned_tabs`へ保存するID markerで、close禁止を意味しない。tabを閉じてもpin markerは維持し、同じ画像を再度openした時に復元する。
- source image data自体はlocalStorageへ保存しない。reload時のlive index recordを使う。

## 11. Modal

### BR-MOD-001 Entry/order

- `selectedIndex !== null` で表示。
- client-filter subsetがあればそのID順。
- なければsearch order。
- sparse slot未fetchならLoading dialogを出し、selected周辺±2 pageをfetch。

### BR-MOD-002 Navigation

- configurable Previous/Next key。既定Left/Right。
- edge click。
- swipe。
- orderの端でwrap。
- sparse searchではabsolute full-order indexから目的方向のpageを必要時だけfetchし、未load slotを飛び越えない。同じpageの同時requestはdedupeしてawaitする。
- resolver開始後にquery、sort、random seed、date、hidden folders、folder set、index token、search generation、active client filterが変わった場合、古い結果で移動しない。
- move後にSeen。
- flipをreset。
- pan/swipeをreset。
- zoom倍率は画像間で維持。

### BR-MOD-003 Edge zone

- default: 左右各28%。
- settings range: 10〜40%。
- left: previous。
- right: next。
- center image single click: chrome toggle。
- center empty click: close。
- single actionはdouble click判定のため約180ms遅延。

### BR-MOD-004 Swipe

- left swipe: next。
- right swipe: previous。
- distance threshold: `clamp(width * 0.16, 72, 180)`。
- またはvelocity 0.55 px/ms以上。
- 小さく遅いdragはnavigationしない。

### BR-MOD-005 Zoom/pan

- wheel: 0.9 / 1.1倍。
- key: 1.15倍。
- range: 25%〜1000%。
- reset: 100%。
- zoom > 100%で画像上pointer dragをpan。
- zoom <= 100%でpanをclear。
- pinch zoomは現行未実装。
- modal close時はzoom reset。

### BR-MOD-006 Chrome/metadata

- center image single clickでtopbar/zoom/edge chromeをhide/show。
- image double clickでmetadata sidebarをtoggle。
- metadata sidebarはmodal openごとにcollapsedから開始。
- 画像navigation中は開閉状態を維持。
- chrome hidden中もmetadata sidebarは独立。
- chrome hidden中のfavorite feedbackは約650ms表示。

### BR-MOD-007 Toolbar

- filenameとcurrent/total
- Favorite +1/-1
- horizontal flip
- Open external
- Enhance start/progress/cancel/error
- enhanced version select
- Original/Enhanced toggle
- enhanced output delete
- source Recycle
- metadata sidebar
- close
- zoom percent/reset

### BR-MOD-008 Metadata

Tabs:

- Prompt
- Negative
- Settings

PNG Infoは常時表示し、copyを持つ。Prompt tagはcomma分割、bracket trim、case-insensitive dedupe、2文字以上、newlineなし、最大160。Tag clickはmodalを閉じ、searchにduplicateでなければ追加する。

### BR-MOD-009 Close

- Escape相当のconfigured key
- close button
- backdrop
- center empty click

Close時はcurrent画像をgalleryの約35%位置へrevealし、selection/right previewを維持する。

### BR-MOD-010 Fixed keys

次は現行で設定変更対象外。

- E: Original/Enhanced
- Space: metadata sidebar
- Enter: Open external
- Ctrl/Cmd+Shift+T: closed tab restore

## 12. Source Delete

### BR-DEL-001 Safety guards

`DELETE /api/delete?path=...` は順に確認する。

1. path必須。欠落400。
2. project root配下ではない。配下なら403。
3. active process indexにabsolute path identityが一致。Windowsではcase-insensitive、他platformではcase-sensitive。なければ403。
4. fileが存在。なければ404。
5. supported extension。違えば415。
6. lexical pathだけでなくreal pathもproject root配下ではない。symlink/junction aliasで配下へ解決される場合も403。

### BR-DEL-002 Recycle Bin only

- Windows `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` のRecycleOptionを使う。
- pathはPowerShell commandへUTF-16 base64 encodingして渡す。
- timeout 30秒。
- hard-delete fallback禁止。
- Recycle失敗時は500とし、indexから消さない。

### BR-DEL-003 Confirmation

- `confirmBeforeDelete` default true。
- ONならalertdialog。
- Cancelは無変更。
- `Do not ask again` 選択で設定をfalseへ保存。
- Modal、global shortcut、bulkで同じ意味を使う。

### BR-DEL-004 Neighbor continuation

Modal/current orderで削除前に候補を決める。

1. 削除位置に次画像が残るならその画像。
2. 削除が末尾なら1つ前。
3. 残り0ならmodal close。
4. API failureならcurrent画像のまま。

このMUSTは完全なcurrent filtered orderを基準にする。現行Browserはabsolute search indexをたどり、必要pageをawaitして削除slotのnext、なければpreviousを解決する。query/window/filterが途中で変わればstale結果を捨て、fetch不能なら削除済みblank modalを残さずclose/clearする。Delete API failure/例外後もnavigation lockを解除する。

### BR-DEL-005 Client reconciliation

成功後:

- sparse/search resultから除去
- totalを1減らす
- browser-owned揮発UI参照からpathを完全に除去する。対象はselection、selection anchor、modal order、right preview cache、open preview tabs、active preview、pin、closed-tab stack/cache、reveal request
- `pvu_preview_tabs` と `pvu_pinned_tabs` も成功応答の処理中に同期更新し、直後のreload、Pin、Restore Last Closedで削除済みpathを復活させない
- page 0をrefresh
- memory indexと該当disk indexから除去
- thumb/display derived cacheをbest-effort cleanup

Source Recycleの所有権はsource file、index、derived thumb/display cache、browser-owned揮発UI参照まで。次は別の共有履歴または別削除権限なのでcascade deleteしない。

- Favorite level: Browser/WPFのmulti-owner shared stateであり、同一pathが再生成された場合の履歴として保持する
- Seen: Browser/WPF間のadditive履歴として保持する
- Enhancement job/output: job audit historyとmanaged outputはSource Recycleとは別の削除権限。新しいfilesystem scan、output削除、job削除をSource Recycleから開始しない
- Scroll memory: image pathではなくview/search context keyの位置なので変更しない

Recycle API failureでは上記のindex、UI、persisted tab/pin、Favorite、Seen、Enhancement、Scrollのどれも変更しない。

### BR-DEL-006 Bulk

- selected pathを同じguarded deleteへ逐次送る。
- 個別failureがあっても残りを処理できる。
- action開始時にunique selected snapshotと確認件数を固定し、re-entryを拒否する。
- 各success pathは`BR-DEL-005`によりselectionから外し、failure pathは選択したままretry可能にする。全体clearは禁止。
- Right PreviewとPage global Delete shortcutは同じ逐次helperとprogress/result文言を使い、success/failure countを表示する。
- Right PreviewとPage global Recycleは進捗とsuccess/failure countをvisible `aria-live` statusで表示する。Cancelは無変更、完了後はpersistent controlへfocusを戻す。

### BR-DEL-007 Enhanced output delete

Source Deleteと別機能。Succeeded jobのmanaged output root内fileだけを削除し、sourceを変更しない。

## 13. Enhancement

### BR-ENH-001 Explicit action isolation

Job creationを許すのは次だけ。

- Enhance button
- configured Enhance shortcut
- `POST /api/enhance/jobs`
- explicit Retry

次はjob createもworker startもしない。

- app load
- scan
- search
- thumbnail/display warm
- single/double click
- right preview表示
- modal open/navigation
- GET jobs/isolation

### BR-ENH-002 Job model

Status:

- queued
- running
- succeeded
- failed
- canceled
- deleted

JobはsourceId、source signature `{ size, mtimeMs }`、preset/settings、adapter、progress、timestamps、outputPath、runId、worker/heartbeat、diagnosticsを持つ。

```ts
type EnhancementJob = {
  id: string;
  sourceId: string;
  sourcePath: string;
  sourceSignature: { size: number; mtimeMs: number };
  presetId: string;
  presetHash: string;
  preset: EnhancementPreset;
  adapterId: string;
  status: "queued" | "running" | "succeeded" | "failed" | "canceled" | "deleted";
  progress: number;
  outputPath?: string;
  errorMessage?: string;
  createdAt: string;
  updatedAt: string;
  startedAt?: string;
  finishedAt?: string;
  cancelRequested?: boolean;
  runId?: string;
  workerInstanceId?: string;
  lastHeartbeatAt?: string;
  externalPromptId?: string;
  externalProcessId?: number;
  diagnostics?: EnhancementDiagnostics;
};
```

`EnhancementDiagnostics` はbackend/model、warningLevel、source/work/target/output dimensions・MP、requested/native scale、upload/queue/engine/download/postprocess/total timing、notesを任意fieldとして持つ。

### BR-ENH-003 Presets

- anime-sharp-x2
- anime-detail-x4
- photo-natural-x2
- photo-detail-x4
- general-balanced-x4
- general-max-x6

Adapters:

- sharp-test
- realesrgan-ncnn
- comfyui

### BR-ENH-004 Settings validation

- ncnn scale 1〜4
- other adapter scale 1〜8
- denoise/sharpen/detail/smooth 0〜100
- brightness/contrast/saturation -100〜100
- format png/webp/jpg
- sourceはactive index必須
- source signatureを実行直前に再確認

Large workはdiagnosticsを返し、confirmation threshold以上なら `confirmLargeJob` なしで409、hard limit超過も409。

### BR-ENH-005 Queue

- singleton in-process queue
- worker loop 1本、job serial
- live queuedをcreatedAt FIFOでclaim
- runId fencing
- queued cancelは即canceled
- running cancelはcancelRequested
- restart後persisted runningは次のqueue start時にfailed(interrupted)
- disk上のqueued jobはrestart直後に自動resumeしない

### BR-ENH-006 Queue UI

- job/errorがない時はqueueOpenでもpanelを描画しない。
- active jobがある時は1秒poll。
- newest first、上位8件。
- queued/running: Cancel。
- failed/canceled: Retry。
- succeeded: Open output、Delete output。
- 全job: Source。
- Refresh、Hide。
- Modalはcurrent sourceのsucceeded outputを検出するとEnhancedを表示候補にする。
- Original/Enhancedの表示選択は画像ごとのin-memory stateで、reload persistenceしない。

### BR-ENH-007 Output isolation

- outputは `.cache/enhance/outputs/**`。
- source path/signature、preset hash、adapterからmanaged pathを決める。
- sourceを上書きしない。
- cancel/stale run時のpartial outputはcleanup。
- output deleteはmanaged root containmentを必須にする。
- 同じmanaged output pathを共有する他のSucceeded jobも、output削除時にDeletedへ更新する。

### BR-ENH-008 Adapter execution

`sharp-test`:

- Local Sharp/Lanczos resizeとfilter/postprocessを使う。
- 複数のcancel checkpointを持つ。
- sourceとは別のmanaged outputへ書く。

`realesrgan-ncnn`:

- 既定adapter。
- executable/model pathをenvironment/configから検証し、不足時は503。
- child processは`shell: false`。
- native 2x/3x/4xを選び、最後にrequested exact sizeとoutput formatへpostprocessする。
- cancelは約500ms間隔で監視し、Windows process treeを終了する。
- stderr tail、PID、timing diagnosticsを保持する。
- temp PNG/native output/final conversionをcleanupする。

`comfyui`:

- 既定URLは`http://localhost:8188`。environmentでremote URLも設定可能。
- workflow/model configを必須にする。
- source upload、workflow patch、prompt enqueue、history poll約1秒、download、exact-size postprocessの順。
- timeout 0は無期限。設定するtimeoutは最低30秒。
- prompt ID取得後は対象prompt/queueをguardしてcancelする。
- prompt ID取得前はglobal `/interrupt` を呼び得るため、他のComfy workを巻き込む可能性がCURRENT LIMITATION。

### BR-ENH-009 ncnn size guard

このMP guardは現行ではReal-ESRGAN ncnn経路にだけ適用する。`workMP` はAI engineへ渡す面積、`targetMP` は最終出力面積。

| Family | blocked | explicit confirmation | slow warning |
| --- | --- | --- | --- |
| all | targetMP > 160 | — | — |
| anime | workMP > 120 | workMP > 80 または targetMP > 120 | workMP > 40 または targetMP > 80 |
| photo/general | workMP > 60 | workMP > 40 または targetMP > 120 | workMP > 20 または targetMP > 80 |

- blocked: 409 `UPSCALE_TOO_LARGE`。
- confirmation: `confirmLargeJob !== true` なら409 `UPSCALE_REQUIRES_CONFIRMATION`。
- confirmed requestだけenqueueする。

## 14. Settingsとkeyboard

### BR-SET-001 Defaults

View defaults:

| Setting | Default |
| --- | --- |
| viewMode | grid |
| thumbnailSize | 200 |
| aspect | original |
| displayStyle | standard |
| columns | 0 / auto |
| sidebar | shown |
| right preview | shown |
| right width | 320 |
| sort | newest |
| folder sort | name-asc |
| modal edge ratio | 0.28 |
| enhancement queue | open |
| date | empty |
| hidden folders | empty |
| Unseen dots | OFF |

Behavior defaults:

- Confirm before delete: ON
- Unseen dots: OFF

保存ownerは分かれる。

- Confirm before deleteとconfigurable key bindings: shared `/api/settings`
- Unseen dots、modal edge ratio、panel/view値: browser local `pvu_view`

### BR-SET-002 Configurable bindings

| Action | Default |
| --- | --- |
| Next | ArrowRight |
| Previous | ArrowLeft |
| Favorite +1 | F |
| Favorite -1 | U |
| Source Recycle | Delete |
| Close modal | Escape |
| Flip | H |
| Enhance | A |
| Zoom in | = |
| Zoom out | - |
| Zoom reset | 0 |

Settings cellをclick後、次のkeydownをdraftへ記録する。

- printable keyはcase-insensitiveで比較し、同じnormalized keyを複数actionへ割り当てたら各fieldにinline conflictを表示する。
- conflict中はSaveをdisabledにし、shared settingsへ書かない。
- `Save key bindings`でconflict-free draftだけをcommitする。
- dialog close/backdrop/Escapeは未保存draftを破棄し、現在保存済みbindingへ戻す。
- `Reset to defaults`はdraftを既定値へ戻し、Saveするまでは永続化しない。

### BR-SET-003 Shortcut guard

次のいずれかならgallery global shortcutを通さない。

- modal/dialog/alertdialogがactive
- input、textarea、select、button、contenteditableへfocus
- modifier付きで別browser/app commandを示す
- pointer操作中のinteractive control

### BR-SET-004 Runtime / Version diagnostics

Landing/scanningとViewerのどちらからも開けるSettingsで、開いている時だけ`GET /api/runtime`を`no-store`で取得するread-only surface。通常のBehavior/key binding設定を待たせたりdisableしたりしない。

- Productは`PhotoViewer`。
- Source revisionは先頭10文字を表示し、full revisionをtitleに保持する。Source stateはlauncher値`1`を`Dirty`、`0`を`Clean`、未設定/不正値をAPI `null`から`Unavailable`として別表示する。
- Build ID、Build completed UTC、通常launcherの`127.0.0.1:<port>`を表示する。
- launcher外起動で取得不能なnullable fieldとSource stateは`Unavailable`とし、Settingsを閉じない。
- loading、invalid payload、HTTP/fetch failureはsection内に表示し、Reloadで再試行できる。
- close/unmount/reloadは前requestをabortし、古いresponseで新しい表示を上書きしない。
- Copy diagnosticsは上記のvalidated fieldとbrowser user agentだけをplain textへ出す。absolute project path、user folder、state/cache path、token、environment、process IDを含めない。
- clipboard成功/拒否はSettingsを閉じず、aria-liveで通知する。
- Runtime表示/Reload/Copyはsettings、localStorage、shared JSON、Enhancement stateを変更しない。

## 15. Persistence

### BR-PER-001 localStorage

`pvu_view` value schema:

```ts
type ViewSettings = {
  viewMode: "grid" | "list";
  thumbSize: number;
  aspectMode: "original" | "square" | "portrait";
  displayStyle: "standard" | "compact" | "poster";
  columns: number; // current reader always normalizes to 0 / auto
  sidebarOpen: boolean;
  rightPanelOpen: boolean;
  rightPanelWidth: number;
  sortBy: "newest" | "oldest" | "created-newest" | "created-oldest" | "name" | "random";
  randomSeed: string;
  folderSortBy: "name-asc" | "name-desc" | "count-desc" | "count-asc";
  modalEdgeRatio: number;
  enhanceQueueOpen: boolean;
  dateFrom: string;
  dateTo: string;
  hiddenFolders: string[];
  showUnseenMarkers: boolean;
  foldersExpanded: boolean;
};
```

Reader normalization is field-local: one malformed field does not discard other valid preferences. Only own data properties are read; inherited values and accessors are ignored. Enums use the allowlists above, booleans must be actual booleans, and dates must be empty or a real `YYYY-MM-DD` calendar date. Numeric bounds are `thumbSize` 40..600, `rightPanelWidth` 240..900, and `modalEdgeRatio` 0.10..0.40; finite out-of-range values clamp while non-numbers/NaN/Infinity fall back. `columns` always becomes 0. `randomSeed` is a non-empty trimmed string of at most 256 characters. `hiddenFolders` keeps at most 500 non-empty strings of at most 4096 characters, case-insensitively deduplicated. Unknown properties are ignored without deleting the stored key wholesale.

| Key | Meaning | Write |
| --- | --- | --- |
| `pvu_favorites` | path -> level | favorite changeでimmediate |
| `pvu_favorites_backup` | non-empty backup | favorite write |
| `pvu_favorites_pending` | exact shared-write journal | favorite change / shared PUT ack |
| `pvu_view` | view/panel/sort/date/hidden/unseen/queue | debounce 300ms |
| `pvu_pinned_tabs` | pin ID set | immediate/effect |
| `pvu_preview_tabs` | ordered open tab IDs + active ID | immediate/effect |
| `pvu_perf_enabled` | profiler | immediate |
| `pvu_fav_only` | Favorites filter | immediate/effect |
| `pvu_unfav_only` | Unrated filter | immediate/effect |
| `pvu_fav_levels` | selected exact levels | immediate/effect |
| `pvu_fav_level` | legacy single level | read fallback |
| `pvu_enhanced_only` | Enhanced filter | immediate/effect |
| `pvu_scroll_memory` | max 80 view positions | debounce 500ms |
| `pvu_seen_images` | additive Seen map | mark時immediate、shared PUTはdebounce 900ms |
| `pvu_last_dir_set` | last folder set | active `dirPath` change |
| `pvu_recent_dirs` | recent set list | scan complete |
| `pvu_enhance_settings` | adapter/preset/controls | on change |
| `pvu_legacy_imported` | legacy migration marker | once |
| `pvu_server_legacy_imported` | server legacy marker | non-empty legacy payloadを実際にmergeした時 |

Favorite変更はbrowser localStorageと`pvu_favorites_pending`へ即時mirrorし、通常は300ms debounce後にdirty pathだけをbase→incoming three-way PUTする。`pagehide`、hidden `visibilitychange`、unmountでは、UTF-8 bodyが60KiB以下かつ別writeがin-flightでない場合に`keepalive` PUTを開始する。Keepaliveはbrowser全体の概ね64KiB queue制約があり完了通知も保証されないため、oversize、in-flight、network failure、close後にresponseを観測できない場合はjournalを残す。次回hydrationはjournalのdirty pathをlocal exact値としてserver GETより優先し、同じdeltaをidempotentに再送する。遅いresponseはjournal revisionが一致する時だけackし、newer changeを巻き戻さない。先行PUTの成功後にnewer revisionがある場合は、そのserver responseを次のthree-way baseへrebaseしてから直列再送する。SeenもlocalStorageへ即時mirrorし、pending additive stateを同じlifecycleでbounded keepaliveする。

Value shapes:

- `pvu_favorites`, `pvu_favorites_backup`: `Record<absolutePath, 1 | 2 | 3 | 4 | 5>`。
- `pvu_favorites_pending`: `{ version: 1, revision, dirtyIds, baseFavorites, baseKnownIds }`。Current levelは`pvu_favorites`から読み、dirty IDがmapに無い場合はexact clearを表す。
- `pvu_pinned_tabs`: `string[]` absolute path IDs。
- `pvu_preview_tabs`: `{ version: 1, tabIds: string[], activeId: string | null }`。supported absolute image pathだけをcase-insensitive dedupeし、最大30件。
- `pvu_perf_enabled`, `pvu_fav_only`, `pvu_unfav_only`, `pvu_enhanced_only`: string flag `"1"` / `"0"`。
- `pvu_fav_levels`: JSON array of exact levels 1〜5。Empty arrayはAll。
- `pvu_fav_level`: legacy scalar value。
- `pvu_scroll_memory`: `Record<viewKey, number>`、insertion orderで最大80。
- `pvu_seen_images`: `Record<absolutePath, true>`。
- `pvu_last_dir_set`: newline formatted folder set。
- `pvu_recent_dirs`: ordered recent folder-set strings。
- `pvu_enhance_settings`: 下記の`EnhancementRequestSettings`。

```ts
type EnhancementRequestSettings = {
  presetId: string;
  adapterId: "sharp-test" | "realesrgan-ncnn" | "comfyui";
  scale: number;
  denoise: number;
  sharpen: number;
  detail: number;
  smoothness: number;
  colorBrightness: number;
  colorContrast: number;
  colorSaturation: number;
  outputFormat: "png" | "webp" | "jpg";
};
```

Enhancement既定はpreset `anime-sharp-x2`、adapter `realesrgan-ncnn`、scale 2、preset由来のdenoise/sharpen/detail/smooth/output format、color値0。

### BR-PER-002 Legacy migration

`pv_*` から対応する `pvu_*` へ、destinationがない時だけcopyする。旧keyは削除しない。

移行suffixの完全な一覧:

- `favorites`
- `favorites_backup`
- `view`
- `pinned_tabs`
- `perf_enabled`
- `fav_only`
- `unfav_only`
- `scroll_memory`
- `seen_images`
- `recent_dirs`
- `last_dir_set`

Favorite levelsやenhance settingsなど、一覧にないkeyは勝手に移行しない。

### BR-PER-003 Shared files

| Path | Schema/meaning |
| --- | --- |
| `.cache/favorites.json` | `Record<absolutePath, 1..5>` |
| `.cache/settings.json` | AppSettings、keyBindings nested |
| `.cache/recent-folders.json` | version 1 RecentFoldersState |
| `.cache/seen.json` | additive `Record<absolutePath, true>` |
| `.cache/index_<hash>.json` | version 1 image index |
| `.cache/folders_<hash>.json` | version 2 folder signatures |
| `.cache/thumbs/**` | derived WebP thumbs |
| `.cache/display/**` | derived WebP displays |
| `.cache/enhance/jobs.json` | version 1 EnhancementJob[] |
| `.cache/enhance/outputs/**` | managed outputs |

Browser SeenはlocalStorageを即時UI用のlocal mirrorとして残しつつ、WPFと `.cache/seen.json` をadditive unionする。shared fileがmalformedなら自動修復や全置換をせず、元fileを保持する。

Index value schema:

```ts
type CacheData = {
  version: number;
  dirPath: string;
  files: Record<string, {
    mtime: number;
    size?: number;
    createdAt?: number;
    metadata: SDMetadata | null;
  }>;
  lastScan: string; // ISO timestamp
};
```

Enhancement storeは `{ version: number, jobs: EnhancementJob[] }`。

### BR-PER-004 Persistしないstate

現行では次をreload restoreしない。

- current search query
- current selection / primary image
- active modal
- Original/Enhancedの画像ごとの表示選択

Folder setはlast/recentとして保存する。Folder section collapseは`pvu_view.foldersExpanded`へ、open preview tab order/active IDは`pvu_preview_tabs`へ、pin IDは`pvu_pinned_tabs`へ保存する。reload後はcurrent resultに存在するtabだけを復元し、modalを自動openしない。

### BR-PER-005 Concurrency

- favorites/settings/recent/seenは同一targetごとのprocess間lock、stale lock recovery、bounded retryを持つ。
- favoritesは`baseFavorites`を使うthree-way merge、settingsはknown fieldのlatest-disk partial merge、recentはset merge、seenはtrue-marker unionをlock内で行う。
- malformed/unsupported shared documentは上書きせず、409とrecoverable errorにする。
- valid writeはtemp fileからrenameし、無関係entryとunknown settings fieldを保持する。
- indexはscan runのpublish boundaryを持つが、cross-process revision storeではない。
- thumb/displayはprocess内generation coalescing + temp rename。enhancement jobsはprocess内write serialization + temp rename。

CURRENT LIMITATION: lockは同一machine filesystem上の協調processを想定し、transaction logやrevision IDを持たない。強制終了直後はstale lock回復待ちがあり、index/thumb/enhancement jobはfavorites等と同じthree-way contractではない。

## 16. Local API contract

### BR-API-001 Common

- JSON errorは原則 `{ error: string }`。
- Pathはabsolute local path。
- APIはlocal trusted client前提。
- Unknown/invalid値の扱いはendpointごとに異なる。Enhancement adapter/rangeと外部POSTの明示unknown presetは400、unknown sortはnewest、settingsのunknown fieldは保持され得る。既存queue/jobを読む内部recoveryだけはlegacy presetをdefaultへfallbackできる。一般化せず各endpoint契約へ従う。
- Viewer data/file endpointの`indexToken`はscan完了で発行されたopaque session selector。token付きrequestはそのsnapshotだけを参照し、別windowのfallback active indexへ混ざらない。

### BR-API-005 Runtime provenance

`GET /api/runtime`

- no-store JSON。
- product、source revision、dirty state、build ID、build完了時刻、server host/port/start時刻、served process IDを返す。
- absolute project root、user path、token、environment全体は返さない。
- launcher外から起動したserverでは取得不能fieldをnullで明示する。dirty stateは`PVU_SOURCE_DIRTY=1`だけtrue、`0`だけfalse、未設定/不正値はnull。

### BR-API-010 Browse

`POST /api/browse?multi=1`

- bodyなし
- 200: `{ path: string, paths: string[] }`
- cancel: empty paths
- picker経路全失敗: 500

### BR-API-020 Scan

`GET /api/scan?dir=<newline-set>&full=1`

- `dir` 必須。欠落400 JSON。
- 以降HTTP 200 SSE。
- events: progress / complete / error。
- partial root failureはcomplete可能。
- all failureはerror event。
- complete eventは`indexToken`を返す。

### BR-API-030 Search

`GET /api/search`

Query:

- `q`
- `page >= 0`
- `size 1..200`、default 100
- `sortBy`
- `randomSeed`
- `dateFrom`
- `dateTo`
- `dir`
- `hiddenFolders` JSON string array
- `indexToken`

200 `SearchResponse`: `{ results: ImageFile[], total, page, totalPages }`。Malformed hiddenFoldersは無視、unknown `sortBy` はnewest。
tokenが既にTTL/LRU失効している場合は410 `{ error }`。clientはこれをtransient search failureと分け、current folder setを使うRescan actionを表示する。tokenなしは互換fallback index。

### BR-API-040 Folders/tags

- `GET /api/folders?dir=...&indexToken=...`: 200 `{ folders: Array<{ key, label, count }> }`、max 200。dirなしはempty。
- `GET /api/tags?indexToken=...`: 200 `{ tags: Array<{ tag, count }> }`、max 2000。

### BR-API-050 Image

`GET /api/image?path=...&thumb=true|display=true&warm=true&priority=...&v=...&indexToken=...`

- missing path: 400
- tokenなしactive index外: 403
- token付きsession index外: 404。fallback active indexへescapeしない
- missing file: 404
- unsupported: 415
- stream: 200
- thumb warm request: success/failureとも204
- display生成失敗: originalへfallback
- `display=true` と `thumb=true` が同時ならdisplayを優先。
- thumb生成失敗もwarm以外ではoriginalへfallback。
- priority mapping: focused/current/modal = -1、high/visible = 0、normal default = 1、nearby/low/preload/warm default = 2。
- version一致derived/original: `max-age=31536000, immutable`。
- version不一致: `max-age=0, must-revalidate`。
- versionなしderived: 1日、versionなしoriginal: 1時間。
- `Content-Length`、`Last-Modified`、size+mtime由来`ETag`を付ける。
- 現行routeはconditional 304とRange responseを実装しない。

`v` はcache busting。Source内容を変更しない。

### BR-API-060 Warmup

- `GET /api/thumbs/warm`: `{ warmup }`
- `POST /api/thumbs/warm`: body `{ dir?, paths?, priority?, limit?, indexToken? }`
- explicit paths利用時はdir必須。欠落400。
- token付きexplicit pathはcaller指定dir containmentとsession index membershipを両方guardする。tokenなしlegacy requestはdir containmentだけ。
- explicit direct warmは最大500 path。
- background/index warmのlimitは最大5000、default 5000。
- success 200 `{ ok: true, warmup }`。
- warmup stateは `{ running, total, queued, completed, failed, startedAt, updatedAt, source, current }`。

### BR-API-070 Open

`POST /api/open?path=...&indexToken=...`

- missing 400
- tokenなしactive index外 403
- token付きsession index外 404
- missing file 404
- unsupported 415
- success 200 `{ success: true }`
- OS open failure 500

### BR-API-080 Delete

`DELETE /api/delete?path=...&indexToken=...`

- missing 400
- lexicalまたはreal pathがproject root配下 403
- 指定session/active index外 403
- missing file 404
- unsupported 415
- success 200 `{ success: true, deletedTo: "recycle-bin" }`
- Recycle failure 500
- successはSource Recycleの権限だけを表し、Favorite、Seen、Enhancement job/outputの削除権限を含まない

### BR-API-090 Favorites

- `GET /api/favorites`: `{ favorites, malformed, error? }`。file不存在はempty map。parse/schema failureは元fileを保持して`malformed: true`。
- `PUT /api/favorites`: body `{ favorites: Record<absolutePath, level> }`。Raw mapをbody直下へ送らない。
- 既に読んだshared mapがあるclientは`baseFavorites`も送る。serverはbase→incomingで変化したkeyだけをlatest disk mapへ適用する。
- Request JSON、`favorites` field、map shape、各valueを検証する。invalid requestは400で既存fileを変更しない。
- 既存shared fileがmalformedなら409で上書きを拒否する。
- valid mapはlevelを0〜5へnormalizeし、latest disk mapへthree-way mergeした結果をtemp fileからrenameする。200 `{ ok: true, favorites, malformed: false }`。
- 同一target writeはprocess間lockで直列化する。lock取得不能は503で既存fileを変更しない。

### BR-API-095 Seen

- `GET /api/seen`: `{ ok, seen, malformed, error? }`。file不存在はempty true-marker map。
- `PUT /api/seen`: body `{ seen: Record<absolutePath, true> }`。false、truthy文字列、missing fieldは400。
- lock内でlatest disk mapとincomingをunionし、temp+renameする。削除・false化・全置換はしない。
- malformed/unsupported existing fileは409で上書きを拒否する。lock取得不能は503。

### BR-API-100 Settings

- `GET /api/settings`: default補完済み`keyBindings`、`confirmBeforeDelete`、`malformed`、任意`error`。file不存在はdefault、parse/known-field schema破損は元fileを保持して`malformed: true`。
- `PUT /api/settings`: `confirmBeforeDelete`または`keyBindings`を含むpartial object。invalid JSON/body/known valueは400で既存file不変。
- 既存shared fileがmalformedなら409で上書きを拒否する。
- top-levelはshallow merge、`keyBindings`はnested merge。未知future fieldは保持する。
- valid documentはtemp fileからrenameし、200 `{ ok: true, ...settings, malformed: false }`。

### BR-API-110 Recent/legacy

- `GET /api/recent-folders`: `{ ok, recent, malformed, error? }`。parseまたはversion 1 known-field schema破損でも元fileを保持して200 `malformed: true`。
- `PUT /api/recent-folders`: bodyはbounded `{ recentDirs?: string[], lastDirSet?: string }` で最低1 field必須。各stringはnewline区切りfolder set。invalid JSON/bodyは400で既存file不変。
- Routeがversion 1 shared schemaへbuildし、保存直前の既存recentとmergeする。既存malformedは409。valid documentはtemp fileからrenameし、成功200。
- `GET /api/legacy-state`: 常に200 `{ recentDirs: [], lastDirSet: "" }`。

### BR-API-120 Enhancement presets/isolation

- `GET /api/enhance/presets`: `{ presets }`
- `GET /api/enhance/isolation`: `{ metrics: { enhancementEnqueues, enhancementWorkerStarts }, queueRunning }`
- isolation metricsはprocess memoryで、server restart時にresetする。

### BR-API-130 Enhancement jobs

`GET /api/enhance/jobs?sourceId=...`

- newest-first `{ jobs }`

`POST /api/enhance/jobs`

```ts
{
  sourceId: string;
  indexToken?: string;
  presetId?: string;
  adapterId?: string;
  scale?: number;
  denoise?: number;
  sharpen?: number;
  detail?: number;
  smoothness?: number;
  colorBrightness?: number;
  colorContrast?: number;
  colorSaturation?: number;
  outputFormat?: "png" | "webp" | "jpg";
  confirmLargeJob?: boolean;
}
```

- invalid JSON/type/config/range: 400。Enhancement settingsはnested `settings` objectではなくbody直下。
- `presetId`省略時はdefault preset、known `presetId`は指定presetを使う。明示unknown `presetId`は400 `Unknown enhancement preset: <id>`で、job作成、queue start、worker start、isolation metric変更を一切行わない。
- persisted legacy jobのretry/recoveryは保存済みresolved presetを内部`createJob`へ渡すため、外部POST境界のunknown拒否で既存queue recoveryを壊さない。
- source指定session/active index外: 404。token付きrequestはfallback indexへescapeしない
- backend unavailable: 503
- too large: 409 `UPSCALE_TOO_LARGE`
- confirmation required: 409 `UPSCALE_REQUIRES_CONFIRMATION`
- accepted: 202 `{ job, diagnostics }`

### BR-API-140 Enhancement job detail/actions

- `GET /api/enhance/jobs/:id`: 200 `{ job }` / 404
- `POST /api/enhance/jobs/:id/cancel`: 200 `{ job, interruptWarning }` / 404
- `POST /api/enhance/jobs/:id/retry`: originalを更新せず新しいjobを作り、202 `{ job }`。404 job/source、409 invalid terminal。source signature変更時は409 code `SOURCE_CHANGED_RECREATE_JOB`。
- `DELETE /api/enhance/jobs/:id/output`: 200 `{ job }`、404、409 unfinished、400 unmanaged/refused。

### BR-API-150 Enhancement output

`GET /api/enhance/output?jobId=...`

- missing 400
- missing job/output/file 404
- managed root外 403
- stream 200

## 17. Loading、empty、error

### BR-ERR-001 Loading

- Landing scan progress。
- Viewer search spinner/placeholder。
- sparse modal Loading。
- imageはcache pendingを共有し、失敗時original fallback。
- enhancementはjob status/progress。

### BR-ERR-002 Error surfaces

- Landing picker、scan、search、bulk Delete、enhancementはblocking alertではなく各workflow内のinline statusを使う。
- scan errorはfolder inputを保持してLandingへ戻り、原因、Retry、Dismissを持つ。
- search errorは直前の成功resultを保持する。結果がある時はgrid上部のnon-modal notice、結果が0件ならempty領域内noticeとし、Retry、Dismissを持つ。
- search 410は`Session expired`として分類し、Retryを表示しない。`Rescan folder set`はcurrent dir setでscanしfresh tokenを得るまで既存galleryを保持する。Favorite/Seen/shared cache/Enhancement queueをresetしない。
- normal zero-resultとrequest failureは別state/copyにする。
- retry中は同じquery/page/filter generationを再発行し、stale responseを捨てる。failureでspinnerを永久表示しない。
- stale 410はnewer generationのsuccess/tokenを上書きしない。
- malformed shared Favorite/Seen/Settings/Recentは既存fileを保持し、書込み拒否理由をrecoverable statusとして扱う。
- single modal Delete failureはsourceとcatalogを保持する。right bulk Deleteは成功/失敗countを表示する。
- folder buckets/tags suggestion取得失敗はmain galleryを壊さず、空fallbackを許す。

CURRENT LIMITATION: app全体の単一notification centerはなく、surfaceごとのinline statusとconsole diagnosticが混在する。別実装はsource action、原因、retry可否を一貫表示してよい。

### BR-ERR-003 Empty

Query empty、filter empty、folder no imageを別copyで識別する。Enhancement queueはjob/errorがなければ非表示。

## 18. Responsive/touch

### BR-RSP-001 Breakpoint

主要breakpointは `max-width: 768px`。

- Header logo/countを隠す。
- Searchと主要iconを残す。
- Sidebarをbackdrop付きfixed drawer、width `min(88vw, 360px)`。`dialog`/`aria-modal`/name、initial focus、Tab trap、Escape/outside close、opener focus returnを持つ。
- Right previewを隠す。
- Modal metadata toggleを残し、sidebarをbackdrop付きbottom sheet（最大62vh）として開く。同じdialog/focus/Escape/outside close契約を持つ。
- Modal toolbarをwrapし、button約30px。
- Settings/delete dialogをviewport幅へ収める。
- Landing controlsをstack。
- Enhancement queueをedge inset。

### BR-RSP-002 Touch

- Modalはpointer swipe/pan対応。
- pinch zoomなし。
- Gallery touch multi-selectなし。
- Search chip reorderはdesktop HTML5 drag、touch/pen pointer handle、keyboard Alt+Shift+Left/Rightに対応する。Handle外から始めた縦scrollは通常通りpageへ渡す。
- mobile sidebarはdialog role、modal focus trap、Escape/backdrop closeを持つ。filter操作後の自動closeはしない。

Mobileのmetadata toggleはbottom sheetを開閉し、backdropとdialog semanticsを持つ。

## 19. Accessibility

### BR-A11Y-001 Existing requirements

- global `:focus-visible` 2px accent outline。
- modal/settingsにdialog role/name。
- delete確認にalertdialog。
- queue/sidebarにcomplementary label。
- imageにalt。
- modal icon buttonにaccessible label。
- favorite feedbackにaria-live。
- folder collapseに `aria-expanded`。
- Search chipはposition/setsize、focus維持、reorder/remove結果のaria-liveを持つ。
- Settingsはinitial focus、Tab trap、Escape、focus returnを持ち、Runtime Reload/Copyも同じkeyboard sequenceへ入る。
- Runtime loading/copy resultはlive region、failureはrecoverable alert。長いID/timestampはdesktop/mobileともpanel外へoverflowしない。

### BR-A11Y-002 Keyboard/focus contractと残るgap

- Grid card/List row/Folder rowはkeyboard focus可能で、button/option相当のrole、selected/pressed/expanded stateを公開する。
- SearchBarはcombobox/listbox/option semantics、active descendant、keyboard selectionを公開する。
- scan progressはstatus/progressbar、live announcement、value textを持つ。
- Settings/Delete/Modalはinitial focus、Tab/Shift+Tab focus trap、Escape close、openerへのfocus returnを持つ。
- right resize handleはseparator role、orientation、min/max/current value、Arrow/Home/End keyboard resizeを持つ。
- bottom tab activate/pin/closeはnative buttonで、active/pin stateとaccessible nameを持つ。
- action iconはLucide iconを使い、iconだけのcontrolは`aria-label`を持つ。文字記号を視覚iconとして代用しない。

WPFはAutomationName、tab order、focus return、keyboard equivalentをMUST検証する。

## 20. Performance、cancellation、cache

### BR-PERF-001 Browser object cache

- thumbnail object URL cache: 2400 completed entries
- display object URL cache: 160
- shared pending fetch
- last consumer release時だけabort
- LRU eviction時にURL revoke

### BR-PERF-002 Warmup

- focused > visible > nearby priority
- 40ms batch
- 3500ms dedupe
- priority upgrade時はresend可能
- directory/contextを分離
- document visible時だけ、idle約4200ms後にbackground warm
- background warm上限1200は「warming上限」であり、画像index/product件数上限ではない

### BR-PERF-003 Sharp concurrency

- CPU基準で4〜12を目安
- `PV_THUMB_CONCURRENCY` 最終1〜16
- visible slot reserve
- active Sharp自体はbackground cancellationでabortしない

### BR-PERF-004 Large library measurement protocol

別実装は画像をsilent truncateしない。これはMUST。5,000画像fixtureはBrowser code由来の性能閾値ではなく、再実装/WPFのstress protocolとしてSHOULD使う。

構造上のpass条件:

- indexed totalがfixture totalと一致する
- 1,201枚目以降も検索・preview可能
- realized UI itemがviewport近傍へbounded
- cancellation後に古いdecodeがcurrent previewを上書きしない

次は数値を記録するが、hardware/build/fixtureを固定したbaselineを作るまでは合否閾値にしない。

- first usable gallery
- selection-to-preview latency
- search response
- forward/back scroll
- memory

## 21. 受入試験

### BR-ACC-001 Runtime/provenance

Static CLI gate:

1. parser/dispatcher unitで`--help` / `-h`時のprepare、port probe、startが0回。
2. invalid/missing/duplicate/out-of-range `--port`はstart 0回でusage付きerror。
3. explicit busyは指定portのprobe 1回、fallback probe 0回、start 0回。
4. 引数なしは3000から順に最初のavailable portを選び、既存のrange behaviorを維持する。

Given: exact checkoutの`git rev-parse HEAD`を記録し、同じproject rootからclean buildまたはlauncherがstale buildを検出

When: 通常launcherを起動

Then:

1. process command lineとcwd/project rootが記録したcheckoutを指す。
2. `.next/BUILD_ID` とbuild完了時刻を記録する。
3. available portでproduction appが開き、契約feature markerをread-only smokeで確認する。
4. `Get-NetTCPConnection`等でlistenerが`127.0.0.1`または`::1`だけである。
5. `/api/runtime` のsource revision、dirty state、build ID、port、PIDがlauncher logと一致する。
6. `localhost` と `127.0.0.1` は到達でき、active non-loopback interface addressでは接続できない。

UIだけからcommitを推測して合格にしない。通常launcherを使わない手動`next start`はprovenanceとloopback bindを保証しないため、製品runtime acceptanceの対象外。

### BR-ACC-010 Landing/recent

1. 3 folderをpasteし、大小文字duplicateを含める。
2. duplicateを1件へnormalize。
3. Openし、reload。
4. LastとRecent先頭から同じordered setを再度開ける。
5. 既存user recentを全削除しない。

### BR-ACC-012 Explicit scan cancellation

1. fake EventSourceで長時間scanを開始し、Scanning中だけprogress surfaceに`Cancel scan`を表示する。
2. progress受信後にkeyboardでCancelするとEventSource close 1回、phaseは即Landing、progress/errorはclear、非blocking cancellation statusをannounceし、入力済みordered folder setと既存recent/state/cacheは不変。Scanning surface unmount後のfocusはenabledなprimary `Open folder set`へ戻る。
3. 同じCancelを二重実行してもcloseやstate mutationを追加せず、cancel後の遅延progress/complete/error/connection errorはphase、progress、error、`indexToken`、recent completion callbackを変更しない。
4. cancel直後に同じfolder setを再scanでき、新しいEventSourceだけがcurrent runになる。
5. provider unmount/navigation cleanupも同じtransport cancel pathを使い、遅延eventによるstate updateやconsole errorを残さない。
6. server request abortまたはReadableStream cancelはscan signalをabortし、partial `setIndex`を呼ばず、finallyでcoordinator reservationをreleaseして同じcanonical folder setのretryを許す。

### BR-ACC-015 Multi-window index isolation

1. Window Aでfolder set A、Window Bで異なるfolder set Bをscanする。
2. 両方のcomplete eventが異なる`indexToken`を返す。
3. B完了後もAのsearch/folders/tags/count/image previewがAだけを返す。
4. A tokenを付けてB-only pathをimage/open/delete/warmup/enhanceへ渡すと拒否し、B sourceを変更しない。
5. tokenなしlegacy requestはfallback active indexで動作するが、現行clientはtokenを落とさない。
6. 失効tokenのsearchは410と再scan案内になり、別session resultへsilent fallbackしない。
7. Aのsource Delete成功はA snapshotと同じpathを含む他snapshotからも除去し、stale file actionを残さない。
8. 410 noticeのRescanは同じfolder setでfresh tokenを取得し、直前galleryをscan中も保持する。通常500 errorはRetryのまま。

### BR-ACC-020 Sidebar cleanup

1. Quick Searchが存在しない。
2. Today/7d/30d/This yearが存在しない。
3. Foldersは初期展開。
4. collapse後にgallery layoutが壊れない。
5. expandで同じbucket状態へ戻る。
6. reload後もcollapse/expand stateを復元し、invalid旧値は展開へnormalizeする。

### BR-ACC-030 Favorite

Fixtureにlevel 0〜5を各1枚用意。

- All: 1〜5の5枚。
- Lv2: level 2だけ。
- Lv1 + Lv4: 2枚。
- level全OFFへ戻す: 5枚。
- Unrated: level 0だけ。
- F/Uで1段ずつ変化。
- reload後も選択levelと値を維持。
- 遅延server GET後もsession内decrease/offが復活しない。
- Browser A/WPF Bが別pathを同時変更しても両方残る。同じpathのBrowser exact変更はbase→incoming差分としてlatest diskへ適用する。
- malformed shared file、lock timeoutでは元fileをbyte-preserveし、local selection/valueを失わない。

### BR-ACC-040 Seen

1. 未閲覧fixtureでUnseen dots default OFF、dot 0。
2. ONでunseen数と同じdot数。
3. viewport scrollだけではSeen数が変わらない。
4. 1枚clickでその1枚だけdot消失。
5. keyboard/modal navigationでも対象だけSeen。
6. OFFでdot 0だがSeen mapは意味上同じ。
7. ONへ戻して残りunseenだけdot。
8. BrowserとWPFが別pathを同時にSeenへ追加し、shared `.cache/seen.json` に両方残る。
9. malformed shared Seenを上書きせず、Browser local Seenで閲覧を継続する。

### BR-ACC-050 Search/date/folder

- `cat, night` が両token AND。
- filenameまたはPNG promptでhit。
- negative-only textはhitしない。
- date From/ToがCreatedのinclusive day。
- hidden folderがserver resultから消える。
- stale requestが新queryを上書きしない。
- query連打の直後にsort/date/folder条件を変えても、最後のqueryと条件の組だけをcommitし、旧debounce timerが結果を巻き戻さない。
- Search chipはmouse/touch/pen/keyboardで同じ順序へreorderでき、移動後focusとaria-live positionが一致する。Pointer threshold未満、cancel、stale indexでは順序を変えない。
- 3 chipをAlt+Shift+Left/Rightでreorderしてquery order、focus、aria positionを同期し、Delete/Backspaceでfocused occurrenceだけを除去する。duplicate token fixtureでも別occurrenceを誤削除しない。

### BR-ACC-060 Zoom

Desktopで:

1. Sidebar widthを計測し240px。
2. Grid中央付近の画像とviewport offsetを記録。
3. Ctrl+wheel/+/-で40〜600内へ変更。
4. Sidebar width、header text、right panel widthが不変。
5. anchor画像がviewport内に残り、driftが1 card未満。
6. Ctrl+0で200。
7. Listでgallery zoomがList thumbnailを変えない。

### BR-ACC-070 Selection/tabs/modal

- clickはright previewのみ。
- double-clickはpinned tab + modal。
- Ctrl/Shift selection semantics。
- current loaded result内のtab clickでmodal。Result外tabはright previewだけ。
- middle closeとCtrl+Shift+T。
- pointer dragとAlt+Shift+Left/Rightの両方でorder変更し、active/pin/focusを維持。
- reload後もopen order、active、pinを復元し、modalを自動openしない。
- Modal Previous/Nextが端でwrap。
- Closeでcurrent画像をgalleryへreveal。
- center click chrome、double-click metadata、swipe threshold。

### BR-ACC-075 Right preview / tabs

1. Panel default 320px、resizeは240〜900へclampし、reload後にwidthを復元。
2. 768px以下でpanelを非表示。
3. empty時にselection案内。
4. single imageで+1/-1、Open、Enhance、Details。
5. DetailsはPath/Created/Modified/Prompt/Negativeだけで、dimensions/size/Open Tabを表示しない。
6. multi-selectionでcount、Favorite Lv0〜5 exact/+1/-1、uniform/mixed status、Open、Enhance、Recycleと結果message。stale/current-result外選択はFavorite対象にせずunavailable件数を示す。
7. Last tab close後、closed historyがあればcompact Restore surfaceが残りfocusを受ける。復元後はtabへfocus。
8. closed historyが空ならRestore surfaceを表示しない。
9. open order/active/pinをreload後に復元する。Pinはclose防止ではない。
10. current result外tabはright previewを保ち、`Outside current search/filter` statusを出す。Result内へ戻るかactive tabなしでstatusを消す。

### BR-ACC-077 Settings Runtime / Version

1. LandingとViewerの両方からSettingsを開ける。閉じている間は`/api/runtime` request 0。開いた時だけ`cache: no-store`で1回取得し、Reloadは前requestをabortして再取得する。
2. dirty fixtureでPhotoViewer、short/full revision、Dirty、Build ID、UTC build time、`127.0.0.1:<port>`を表示する。
3. nullable provenanceとSource state nullは`Unavailable`。Source stateはtrue=`Dirty`、false=`Clean`、null=`Unavailable`で、copyも同じ三値を使う。invalid/path-shaped/non-loopback payloadとHTTP failureはinline recoverable error。どの場合もBehavior/key binding操作を継続できる。
4. close/unmount後に遅延responseを解決してもstate updateせず、reopen後のnewer responseをstale responseが上書きしない。
5. Copy diagnosticsはvalidated runtime fieldsとuser agentだけを含み、project/user/state/cache path、process ID、unknown API fieldを含めない。clipboard拒否はaria-live error。
6. Close→Reload→Copyへkeyboardで到達し、focus trap/returnを維持する。320px幅相当でも長いrevision/build ID/timeが横overflowしない。
7. Runtime操作前後でkey binding draft/save、Confirm before delete、Unseen dotsの値と保存requestが変わらない。

### BR-ACC-080 Delete

Disposable source copyだけを使用。

1. Cancelでfile/index/selection不変。
2. 3枚のmiddle削除で元の次へ。
3. last削除で前へ。
4. only削除でmodal close。
5. filtered subsetでもそのorderのneighbor。
6. page未load境界をまたぐ削除でもfull query orderの直隣。未load pageをfetchし、stale windowの結果は採用しない。
7. project root、active index外、unsupportedを拒否。
8. Recycle failureでindexを消さない。
9. hard delete fallbackなし。
10. Enhanced output deleteでsource不変。
11. Recycle成功後、selection/modal/right preview/open tab/pin/closed-tab history/revealとpersisted tab/pinの全てからpathが消える。
12. 成功直後にPin、Restore Last Closed、reloadを行っても削除済みpathが復活しない。
13. Favorite、Seen、Succeeded enhancement job/outputは成功後も保持され、Source Recycleから新規filesystem scanやoutput/job deleteを開始しない。
14. partial bulk failureではsuccessだけ選択解除し、failed pathを選択したままcountを表示する。
15. Bulk中はprogress/final success+failure countを画面と`aria-live`へ出し、連打で二重実行しない。

### BR-ACC-090 Enhancement isolation

Given: disposable fixtureと利用可能なadapter。Backend/model依存を避けるcontract testでは`sharp-test`を明示する。ncnnを使うならavailabilityを先に確認し、large threshold未満のfixtureを使う。

1. jobs/isolation snapshotを取る。
2. scan、search、100枚scroll、single/double click、modal 20 move。
3. job count、enqueue count、worker-start countが増えない。
4. 利用可能adapterに対する明示Enhanceだけが202 jobを作る。Unavailable backendの503とlarge guardの409は仕様通りの別結果。
5. cancel/retry/output deleteがsourceを変更しない。

### BR-ACC-100 Persistence

- Favorite/View/Seen/Scrollのdebounce後reloadでbrowser-local値を復元。
- Grid/List、sidebar、Folders collapse、thumbnail size、panel resize、Unseen dotsをflush前に連続変更しても、`pvu_view`は最後のsnapshotだけを保存する。
- Favorite change直後のpagehideでもbounded keepalive PUTを開始し、未ack/oversizeはlocal journalから次回reloadでexact retryする。
- 遅いFavorite PUT responseがnewer level/decrease/clearを巻き戻さない。
- Seenはadditive local mirrorとbounded lifecycle PUTを持ち、response mergeでnewer local markを消さない。
- malformed viewはapp crashせずfield-local normalization。valid fieldと保存keyを維持し、invalid enum/boolean/date/NaN/Infinity/accessor/prototype値だけをdefaultまたは定義済み範囲へ戻す。
- shared recent malformedを無条件上書きしない。
- user state/cache全削除をmigration手段にしない。

### BR-ACC-110 Responsive/a11y

390pxと768pxで:

- horizontal overflowで主要操作が切れない。
- sidebar overlayがgallery widthを永久変更しない。
- right preview非表示。
- modal close/Delete/Favorite/navigationが到達可能。
- focus-visible。
- dialog name。
- icon accessible name。

Browser/WPFのtarget:

- keyboardのみで主要flowを完了。
- dialog close後にfocusを戻す。
- card/row/folderへAutomation/semantic roleを付ける。

現行BrowserはGallery primary button/group、grid card/list row/folder row、Search combobox/chips、preview tabs、dialog、resize separator、scan progress、bottom pin/close、mobile drawerへkeyboard/semantic contractを実装済み。Search chipはmouse/touch/pen/keyboardで同じreorder/remove結果へ到達できる。WPFはAutomationNameとkeyboard equivalentまで検証する。

### BR-ACC-120 Console

Landing → scan → viewer → filters → zoom → preview → modal → settingsのread-only smokeでconsole error/warning 0を目標とする。既知の意図的warningがある場合は要件IDと理由を記録する。

## 22. 現行の既知制約・曖昧点

次は「現行挙動の記録」であり、そのまま再現するMUSTではない。

1. Original gridはmasonryではない。
2. Show selectedはisolate filterではない。
3. Favorite/Seen/enhancement履歴の明示cleanup UIはない。Source Recycle時はmulti-owner/history/別削除権限を守るため意図的に保持し、Pinを含む揮発UI参照だけを成功時にpurgeする。
4. cold processのmerged cache/tagsがactive scan外のhistorical rootを含み得る。
5. Same-name overwriteをincremental signatureで見逃す可能性があり、full scanが回復経路。
6. queued enhancementはrestart直後に自動resumeしない。
7. Comfy cancelはprompt ID取得前だとglobal interruptの曖昧性がある。
8. shared JSON lockはtransaction log/revision IDを持たず、index/thumb/enhancement jobはFavorite等と同じthree-way contractではない。
9. API errorにlocal path/internal detailが含まれ得る。
10. `/api/scan` はcaller指定absolute directoryを走査し、HTTP layerのauthentication/CSRF protectionもない。
11. `/api/thumbs/warm` のtokenなしlegacy explicit pathはcaller指定dir containmentだけをguardし、active index membershipを必須にしない。現行clientのtoken付き経路はsession membershipもguardする。
12. ComfyUIのtimeout既定0は無期限待機を意味する。
13. Browser interaction E2Eはcomponent/unit契約より範囲が狭く、Landing/recent中心の既存suiteに依存する。
14. Sparse resultのgallery range selectionとmodal外keyboard selectionはloaded subsetに制限される。Modal navigation/Delete neighborはfull sparse order resolverを使う。
15. Current result外のPreview tab clickはmodalを開かない。
16. thumb/display/enhance output disk cacheにquota/LRU evictionがない。
17. lifecycle keepaliveはbrowser全体の約64KiB上限を考慮して60KiB以下だけ送る。oversizeまたは既存PUT中にcloseした場合、同じsession内のshared完了は保証せず、durable pending journalを次回hydrationで再送する。

## 23. Source map

基準commitでの主な実装証拠。行番号は将来の編集でずれるため、pathとsymbolを優先する。

| Requirement area | Source |
| --- | --- |
| Runtime/launcher | `package.json`, `scripts/prod_launcher.js`, `scripts/prod_launcher_cli.js`, `scripts/serve_with_parent_watch.js`, `scripts/verify-browser-runtime.ps1`, `src/app/api/runtime/route.ts` |
| Types/default keys | `src/lib/types.ts`, `src/lib/imageFormats.ts` |
| Main state/persistence | `src/store/ImageContext.tsx` |
| Landing/header/bulk/global keys | `src/app/page.tsx` |
| Sidebar/filter/folders | `src/components/Sidebar.tsx` |
| Search chips/suggestions | `src/components/SearchBar.tsx` |
| Gallery/zoom/virtualization | `src/components/ImageGrid.tsx` |
| Right preview | `src/components/RightPreviewPanel.tsx` |
| Bottom tabs | `src/components/BottomPreviewTabs.tsx` |
| Modal/Delete neighbor | `src/components/ImageModal.tsx` |
| Settings / Runtime diagnostics | `src/components/SettingsModal.tsx`, `src/components/RuntimeDiagnosticsSection.tsx`, `src/lib/runtimeDiagnostics.ts` |
| Enhance UI | `src/components/EnhanceQueuePanel.tsx` |
| Search/sort/index/folders | `src/lib/indexer.ts` |
| PNG parse | `src/lib/pngParser.ts` |
| Thumbnail cache | `src/lib/thumbnailCache.ts` |
| Client image cache | `src/lib/clientImageCache.ts` |
| Warmup batching | `src/lib/thumbnailWarmupBatcher.ts` |
| Favorite/Unseen helpers | `src/lib/browserUiPreferences.ts` |
| Shared file coordination | `src/lib/fileWriteLock.ts`, Favorite/Seen/Settings/Recent API routes |
| Shared Seen | `src/app/api/seen/route.ts`, `src/store/ImageContext.tsx` |
| Preview tab persistence | `src/lib/previewTabPersistence.ts`, `src/store/ImageContext.tsx` |
| Scan run cancellation/conflict | `src/lib/scanRunCoordinator.ts`, `src/app/api/scan/route.ts` |
| Active image trust boundary | `src/lib/activeImagePath.ts`, Image/Open/Delete API routes |
| Dialog focus | `src/lib/useDialogFocus.ts` |
| Modal nav/zoom helpers | `src/lib/modalNavigation.ts`, `src/lib/modalZoom.ts` |
| Selection/key guards | `src/lib/viewerUi.ts`, `src/lib/imageListState.ts` |
| Folder set/recent | `src/lib/pathSet.ts`, `src/lib/recentFolders.ts` |
| APIs | `src/app/api/**/route.ts` |
| Enhance lifecycle | `src/lib/enhance/**` |
| Responsive/layout | `src/app/globals.css` |

### 23.1 Test map

| Contract | Test evidence |
| --- | --- |
| Runtime provenance tri-state / launcher CLI safety | `src/app/api/runtime/route.test.ts`, `src/lib/runtimeDiagnostics.test.ts`, `scripts/prod_launcher_cli.test.mjs`, `scripts/verify-browser-runtime.ps1` |
| Settings Runtime display/copy/stale response | `src/components/SettingsModal.test.tsx`, `src/lib/runtimeDiagnostics.test.ts` |
| Gallery/folder/tab/scan semantics | `src/components/ImageGrid.test.tsx`, `src/components/Sidebar.test.tsx`, `src/components/BottomPreviewTabs.test.tsx`, `src/components/ScanProgressStatus.test.tsx` |
| Favorite levels/Unseen | `src/lib/browserUiPreferences.test.ts`, `src/components/Sidebar.test.tsx`, `src/components/SettingsModal.test.tsx` |
| Favorite shared write safety | `src/app/api/favorites/route.test.ts` |
| Shared Seen union/write safety | `src/app/api/seen/route.test.ts` |
| Cross-runtime Favorite/Seen stress | `src/app/api/crossRuntimeSharedState.worker.test.ts`, `scripts/verify-cross-runtime-shared-state.ps1` |
| Shared file lock | `src/lib/fileWriteLock.test.ts` |
| Settings shared write safety | `src/app/api/settings/route.test.ts` |
| Recent folders shared write safety | `src/app/api/recent-folders/route.test.ts` |
| Favorite merge/view migration | `src/store/ImageContext.test.tsx` |
| Preview tab reload | `src/lib/previewTabPersistence.test.ts`, `src/store/ImageContext.test.tsx` |
| Scan cancellation/conflict/recovery | `src/lib/scanRunCoordinator.test.ts`, `src/app/api/scan/route.test.ts`, `src/store/ImageContext.test.tsx` |
| Modal order/swipe | `src/lib/modalNavigation.test.ts` |
| Modal zoom | `src/lib/modalZoom.test.ts` |
| Gallery arrow math | `src/lib/viewerUi.test.ts` |
| Date | `src/lib/dateFilter.test.ts` |
| Sparse Delete list | `src/lib/imageListState.test.ts` |
| Delete route safety | `src/app/api/delete/route.test.ts` |
| Image/Open active-index boundary | `src/lib/activeImagePath.test.ts`, `src/app/api/image/route.test.ts`, `src/app/api/open/route.test.ts` |
| Source Recycle UI ownership/history retention | `src/store/ImageContext.test.tsx` |
| Partial bulk Recycle retry selection | `src/components/RightPreviewPanel.test.tsx`, `src/lib/bulkRecycle.test.ts` |
| Index/incremental/sort | `src/lib/indexer.test.ts` |
| Scan progress | `src/lib/scanProgress.test.ts` |
| Thumb/cache | `src/lib/thumbnailCache.test.ts`, `src/lib/clientImageCache.test.ts`, `src/lib/thumbnailWarmupBatcher.test.ts` |
| Folder/recent | `src/lib/pathSet.test.ts`, `src/lib/recentFolders.test.ts` |
| Enhancement isolation/store/queue | `src/lib/enhance/jobStore.test.ts`, `src/lib/enhance/queue.test.ts` |
| Landing recent E2E | `e2e/home.spec.ts` |
| A11y/focus surfaces | `src/lib/useDialogFocus.test.tsx`, `src/components/ImageGrid.test.tsx`, `src/components/SearchBar.test.tsx`, `src/components/Sidebar.test.tsx`, `src/components/BottomPreviewTabs.test.tsx`, `src/components/MetadataTabList.test.tsx`, `src/components/RightPreviewPanel.test.tsx`, `src/components/ScanProgressStatus.test.tsx` |

`src/app/api/delete/route.test.ts` はRecycle Bin依存を注入してsourceを一切削除せず、project-root/index/existence/type/real-path guard、Recycle失敗時の非mutation、成功時のindex/cache順序、hard-delete fallback不在を直接検証する。Windows Recycle Binそのもののintegration testは、disposable copyを明示的に用意する実操作gateだけで行う。

## 24. Change control

この仕様を変える時は次を同じchangeで行う。

1. 変更する要件IDと理由をissueへ記録。
2. Browser implementationとfocused testを更新。
3. 通常launcherが選んだproduction portで該当flowを確認。ユーザーが使用中の3000を奪わない。
4. user state/cache削除なしのreload persistenceを確認。
5. [browser-to-wpf-parity-plan.md](./browser-to-wpf-parity-plan.md) のWPF分類と受入条件を同期。
6. Delete、Enhancement isolation、shared state互換へ影響する場合は専用safety testを追加。

この文書だけを見て実装するAIは、CURRENT LIMITATIONを意図せず「正しい仕様」として複製してはならない。MUST、受入試験、Source mapを一組として扱う。
