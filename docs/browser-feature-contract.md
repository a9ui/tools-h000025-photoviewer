# Browser版 PhotoViewer 正規要件・実装仕様書

文書状態: **Normative / implementation-ready**

最終監査日: 2026-07-14 JST

基準リポジトリ: H000025 PhotoViewer

基準commit: `626b7dd5416f3619ae59fc66d47e79acd1a74fd5`（監査時の `origin/main`）

実動作確認: 通常production launcher、`http://localhost:3000/`
関連追跡: GitHub issue #316 / SQLite improvement item #33

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

1. `.next/BUILD_ID` がない、または監視対象source/configがbuildより新しければproduction buildする。
2. Windows上で同じproject rootを参照する古いproject server processを安全に整理する。
3. port 3000から3999までの最初の空portを選ぶ。
4. `next start -p <port>` をparent-watch wrapperで起動する。
5. ready後にbrowserを開く。

3000は既定候補であり、競合時に必ず3000へ固定する契約ではない。監査時の通常launcherは3000で稼働していた。

### BR-RUN-003 Enhancement autostart

ComfyUI autostartは既定OFF。`PVU_COMFY_AUTOSTART=1` の明示時だけlauncherが起動経路を使える。

### BR-RUN-004 Local trust boundary

現行APIにはauthentication、CSRF token、Origin validationがない。`next start` もloopback bindを明示強制していない。したがってMUST:

- Browser版は信頼済みlocal machine用として扱う。
- 外部networkへ公開しない。
- 別実装はこの無認証HTTP境界を互換要件としてコピーしない。
- absolute pathを扱うため、log・diagnostic・screenshotの共有時はprivacyに注意する。

**CURRENT P0 BLOCKER:** 2026-07-14のport 3000 listenerは`Get-NetTCPConnection`で`LocalAddress ::`だった。現launcherはloopback-only bindを保証していない。Firewall等の外側条件をこの仕様の安全証明にしない。Browser製品としてはlauncher/serverを`127.0.0.1`または`::1`だけへbindする修正と受入証拠が未完了。

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

## 4. 画面状態と遷移

### BR-STATE-001 Top-level phase

Top-level phaseは次の3つ。

| Phase | Entry | Exit |
| --- | --- | --- |
| Landing | 起動、logoから戻る、scan failure | folder setをscan |
| Scanning | Open/Refresh | completeでViewer、fatal errorでLanding |
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

- recentは最大8 set。
- set比較はcase-insensitive。
- last setをrecentの先頭へ置く。
- browser local folder memoryが空の時だけshared stateを初期importする。
- active `dirPath`変更時にbrowser-local last setを書き、scan completeでrecentとshared stateを更新する。
- malformed shared stateは勝手に上書きせず、PUTは409で保護する。
- legacy stateはadditive importとし、現行stateを消さない。

CURRENT LIMITATION: shared recent helperのset dedupeはcase-insensitiveだが、browser-local recent listのduplicate除外にはcase-sensitiveな経路がある。別実装はcase-insensitiveへ統一してよい。

### BR-LND-004 Scan start

- 通常click: incremental scan。
- Shift-click: force full verification。
- folder setが空なら開始しない。
- scan中はbuttonをScanning状態にする。

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
- client connection切断は現在server scanをcancelしない。
- global scan mutexはない。

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

UIはfolder progress、処理数、全体percentを表示する。connection lostはalertを出しLandingへ戻る。

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

- server-side query/date/folder filterだけなら `filtered / indexed` を表示できる。
- Favorite、Unrated、Enhancedはclient filterのため、header totalと現在描画card数が一致しない場合がある。
- この不一致はCURRENT LIMITATIONであり、新実装は正確なvisible countを追加してよい。

### BR-SID-001 Folders section

- 見出し `Folders` とfolder countを表示する。
- 既定は展開。
- 見出しbuttonで折り畳み可能。
- collapse状態は現行ではsession localで、reload persistenceなし。
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
- dragでchip順序を変更。
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
- 最大8件。
- ArrowDown/ArrowUp、Enter/Tab、Escape、mouseで操作。
- outside clickで閉じる。

### BR-SRC-004 Debounce/stale request

- SearchBarからcontext反映: 約200ms。
- search fetch: 約150ms debounce。
- 新queryは前requestをAbortControllerでabort。
- generation IDでstale responseを捨てる。

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

「Lv N以上」のthreshold filterは禁止。

### BR-FAV-003 Merge

- Browser localとshared server mapは通常max levelでmerge。
- 現sessionで変更したdirty pathはlocal exact値を優先し、遅いserver GETで削除や減算を復活させない。
- local primaryとbackupがある場合は回復に使う。

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

- Ctrl/Cmd + wheel
- Ctrl/Cmd + `+`
- Ctrl/Cmd + `-`
- Ctrl/Cmd + `0`
- range 40〜600
- reset 200

List:

- browser page zoomは抑止する。
- Grid thumbnail settingでList thumbnailを拡大縮小しない。

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

CURRENT LIMITATION: range selectionとkeyboard orderは`searchResults.filter(non-null)`のloaded ID subsetを使う。Sparseな大規模resultの未load区間をまたぐfull-order range/navigationは保証しない。別実装は全query order modelを使って改善してよい。

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

Detailsは現行では次だけ。

- Path
- Created
- Modified
- Prompt
- Negative

Dimensions、file size、Open Tab buttonは現行契約に含めない。

### BR-RPV-003 Multi-selection

- selected count
- Favorite +1を全件
- Open externalを全件
- Enhance selectedを逐次
- Recycle selectedを逐次
- success/failure message
- 完了後selection clear

### BR-TAB-001 Creation/activation

- Gallery double clickでだけ自動作成し、自動pin。
- single clickは作らない。
- tab clickはactive IDを同期する。Current loaded `searchResults` にtab IDがある場合はselected indexも同期してfull modalを開く。Query/filter変更後などcurrent results外のtabはactive right previewだけを更新し得る。
- tabが1つ以上ある時だけbottom barを表示。

### BR-TAB-002 Operations

- x close
- middle-click/pointer middle close
- hover 220x220 preview + filename/path
- pin marker toggle
- Restore closed
- Ctrl/Cmd+Shift+T
- closed stack最大30

最後のtabを閉じるとbottom barも消えるため、画面上のRestoreは使えない。その状態の復元経路はCtrl/Cmd+Shift+T。

Active tabを閉じた時は残りの末尾をactiveにする。

### BR-TAB-003 Pin limitation

PinはID markerの保存であり、次を保証しない。

- close禁止
- reload後のopen tab集合復元
- image data復元

別surfaceはより強いsession restoreをnative extensionとして提供してよい。

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
3. active process indexにabsolute path完全一致。なければ403。
4. fileが存在。なければ404。
5. supported extension。違えば415。

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

このMUSTは完全なcurrent filtered orderを基準にする。ただし現行Browserのfallback orderは、明示`modalImageIds`がない時にloaded non-null search resultsだけから作るため、page未load境界では本来の直隣を飛ばし得る。通常loaded orderのneighborは実装済みだが、sparse境界は未解決のCURRENT LIMITATION。

### BR-DEL-005 Client reconciliation

成功後:

- sparse/search resultから除去
- totalを1減らす
- selection/active preview/tabから除去
- page 0をrefresh
- memory indexと該当disk indexから除去
- thumb/display derived cacheをbest-effort cleanup

Favorite、Seen、pinned ID、enhancement historyのorphan cleanupは現行では行わない。これはCURRENT LIMITATION。

### BR-DEL-006 Bulk

- selected pathを同じguarded deleteへ逐次送る。
- 個別failureがあっても残りを処理できる。
- 完了後selection clear。
- Right PreviewのRecycle操作はsuccess/failure countを表示する。
- Page global Delete shortcutは逐次処理後にselectionをclearするが、現行ではcount messageを出さない。統一feedbackはSHOULD改善。

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

Settings cellをclick後、次のkeydownを即保存する。現行はSave/Cancel、conflict validation、reset defaultsを持たない。

### BR-SET-003 Shortcut guard

次のいずれかならgallery global shortcutを通さない。

- modal/dialog/alertdialogがactive
- input、textarea、select、button、contenteditableへfocus
- modifier付きで別browser/app commandを示す
- pointer操作中のinteractive control

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
  folderSortBy: "name-asc" | "name-desc" | "count-desc";
  modalEdgeRatio: number;
  enhanceQueueOpen: boolean;
  dateFrom: string;
  dateTo: string;
  hiddenFolders: string[];
  showUnseenMarkers: boolean;
};
```

| Key | Meaning | Write |
| --- | --- | --- |
| `pvu_favorites` | path -> level | debounce 300ms |
| `pvu_favorites_backup` | non-empty backup | favorite write |
| `pvu_view` | view/panel/sort/date/hidden/unseen/queue | debounce 300ms |
| `pvu_pinned_tabs` | pin ID set | immediate/effect |
| `pvu_perf_enabled` | profiler | immediate |
| `pvu_fav_only` | Favorites filter | immediate/effect |
| `pvu_unfav_only` | Unrated filter | immediate/effect |
| `pvu_fav_levels` | selected exact levels | immediate/effect |
| `pvu_fav_level` | legacy single level | read fallback |
| `pvu_enhanced_only` | Enhanced filter | immediate/effect |
| `pvu_scroll_memory` | max 80 view positions | debounce 500ms |
| `pvu_seen_images` | additive Seen map | debounce 900ms |
| `pvu_last_dir_set` | last folder set | active `dirPath` change |
| `pvu_recent_dirs` | recent set list | scan complete |
| `pvu_enhance_settings` | adapter/preset/controls | on change |
| `pvu_legacy_imported` | legacy migration marker | once |
| `pvu_server_legacy_imported` | server legacy marker | non-empty legacy payloadを実際にmergeした時 |

Unmount時はpending Favorite/View/Scroll/Seenをbrowser localStorageへflushする。Favoriteのpending `/api/favorites` PUTまでは強制flushしないため、shared `.cache/favorites.json` は300ms debounce完了前のcloseで遅れ得る。Shared persistenceを検証する時はdebounceとPUT完了を待つ。

Value shapes:

- `pvu_favorites`, `pvu_favorites_backup`: `Record<absolutePath, 1 | 2 | 3 | 4 | 5>`。
- `pvu_pinned_tabs`: `string[]` absolute path IDs。
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
| `.cache/index_<hash>.json` | version 1 image index |
| `.cache/folders_<hash>.json` | version 2 folder signatures |
| `.cache/thumbs/**` | derived WebP thumbs |
| `.cache/display/**` | derived WebP displays |
| `.cache/enhance/jobs.json` | version 1 EnhancementJob[] |
| `.cache/enhance/outputs/**` | managed outputs |

Browser SeenはlocalStorageだけ。WPFの `.cache/seen.json` と自動同期しない。

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
- folder section collapse
- open preview tabの完全な集合と内容
- Original/Enhancedの画像ごとの表示選択

Folder set自体はlast/recentとして保存し、pin IDだけは `pvu_pinned_tabs` に保存する。

### BR-PER-005 Concurrency limitation

- favorites/settings/recent/indexは一般的なcross-process lockやrevisionを持たない。
- favorites/settingsはwhole-file direct write。
- recentは意味上mergeするがtransactionはない。
- thumb/displayはtemp+rename。
- enhancement jobsはprocess内write serialization + temp rename。cross-process lockはない。

BrowserとWPFが同時にshared JSONを更新するとlost updateの可能性がある。WPF側のlock/revision/explicit import設計と受入条件はparity planで決める。Browser互換要件として特定方式を強制しない。

## 16. Local API contract

### BR-API-001 Common

- JSON errorは原則 `{ error: string }`。
- Pathはabsolute local path。
- APIはlocal trusted client前提。
- Unknown/invalid値の扱いはendpointごとに異なる。Enhancement adapter/rangeは400、unknown sortはnewest、unknown presetはfallback、settingsのunknown fieldは保持され得る。一般化せず各endpoint契約へ従う。

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

200 `SearchResponse`: `{ results: ImageFile[], total, page, totalPages }`。Malformed hiddenFoldersは無視、unknown `sortBy` はnewest。

### BR-API-040 Folders/tags

- `GET /api/folders?dir=...`: 200 `{ folders: Array<{ key, label, count }> }`、max 200。dirなしはempty。
- `GET /api/tags`: 200 `{ tags: Array<{ tag, count }> }`、max 2000。

### BR-API-050 Image

`GET /api/image?path=...&thumb=true|display=true&warm=true&priority=...&v=...`

- missing path: 400
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
- `POST /api/thumbs/warm`: body `{ dir?, paths?, priority?, limit? }`
- explicit paths利用時はdir必須。欠落400。
- explicit pathはcaller指定dir containmentだけをguardし、active index membershipは必須でない。
- explicit direct warmは最大500 path。
- background/index warmのlimitは最大5000、default 5000。
- success 200 `{ ok: true, warmup }`。
- warmup stateは `{ running, total, queued, completed, failed, startedAt, updatedAt, source, current }`。

### BR-API-070 Open

`POST /api/open?path=...`

- missing 400
- missing file 404
- unsupported 415
- success 200 `{ success: true }`
- OS open failure 500

### BR-API-080 Delete

`DELETE /api/delete?path=...`

- missing 400
- project root 403
- active index外 403
- missing file 404
- unsupported 415
- success 200 `{ success: true, deletedTo: "recycle-bin" }`
- Recycle failure 500

### BR-API-090 Favorites

- `GET /api/favorites`: `{ favorites }`。read/parse failureはempty map。
- `PUT /api/favorites`: body `{ favorites: Record<absolutePath, level> }`。`favorites` fieldをnormalizeして全置換。200 `{ ok: true, favorites }`。Raw mapをbody直下へ送らない。
- CURRENT LIMITATION: invalid JSONまたは`favorites` field欠落もempty mapへnormalizeし、shared fileを空へ全置換し得る。

### BR-API-100 Settings

- `GET /api/settings`: normalized settings。破損時default。
- `PUT /api/settings`: partial settings。top-level shallow merge、keyBindingsのみnested merge。200 updated settings。
- 現行routeは完全schema validationをしない。別実装はvalidationを追加してよい。
- PUT invalid JSONはcatchされず500になり得る。

### BR-API-110 Recent/legacy

- `GET /api/recent-folders`: `{ ok, recent, malformed, error? }`。malformedでも200。
- `PUT /api/recent-folders`: body `{ recentDirs: string[], lastDirSet: string }`。各stringはnewline区切りfolder set。Routeがversion 1 shared schemaへbuildし、既存recentとmergeする。既存malformedは409。成功200。
- PUT invalid JSONはempty incomingとして扱い、既存recent setを残しながらlastFolderSetをemptyへし得る。
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
- source active index外: 404
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

現行:

- Landing picker: inline。
- scan: alert + Landing。
- search: console + spinner終了。
- folder buckets/tags: silent empty。
- single modal Delete failure: console。
- right bulk Delete: count message。
- enhancement: inline。

統一notification surfaceはCURRENT LIMITATION。別実装はsource action、原因、retry可否を示す一貫したerror UIをSHOULD実装する。

### BR-ERR-003 Empty

Query empty、filter empty、folder no imageを別copyで識別する。Enhancement queueはjob/errorがなければ非表示。

## 18. Responsive/touch

### BR-RSP-001 Breakpoint

主要breakpointは `max-width: 768px`。

- Header logo/countを隠す。
- Searchと主要iconを残す。
- Sidebarをabsolute overlay、width `min(82vw, 300px)`。
- Right previewを隠す。
- Modal metadata sidebarを隠す。
- Modal toolbarをwrapし、button約30px。
- Settings/delete dialogをviewport幅へ収める。
- Landing controlsをstack。
- Enhancement queueをedge inset。

### BR-RSP-002 Touch

- Modalはpointer swipe/pan対応。
- pinch zoomなし。
- Gallery touch multi-selectなし。
- chip reorderのtouch/keyboard完全対応なし。
- mobile sidebarのfocus trap/drawer role/auto-closeなし。

Mobileでmetadata toggleが見えてもCSSでsidebarが出ない点はCURRENT LIMITATION。別実装はdead controlを表示しない。

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

### BR-A11Y-002 Gaps not to copy

次は現行の不足で、別実装の互換要件ではない。

- grid card/list row/folder rowの一部がclickable divで、role/tabIndexなし
- SearchBarに完全なcombobox/listbox semanticsなし
- scan progress semantics不足
- dialog focus trap、initial focus、return focusなし
- Settings/deleteをEscapeで閉じない箇所
- right resize handleのseparator/keyboard semanticsなし
- bottom pin/closeがspan
- mobile drawer semanticsなし
- title属性だけに依存するicon

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

Given: exact checkoutの`git rev-parse HEAD`を記録し、同じproject rootからclean buildまたはlauncherがstale buildを検出

When: 通常launcherを起動

Then:

1. process command lineとcwd/project rootが記録したcheckoutを指す。
2. `.next/BUILD_ID` とbuild完了時刻を記録する。
3. available portでproduction appが開き、契約feature markerをread-only smokeで確認する。
4. `Get-NetTCPConnection`等でlistenerが`127.0.0.1`または`::1`だけである。

現行port 3000は4を満たさず、runtime safety acceptanceは未解決。Version endpointがないため、UIだけからcommitを推測して合格にしない。

### BR-ACC-010 Landing/recent

1. 3 folderをpasteし、大小文字duplicateを含める。
2. duplicateを1件へnormalize。
3. Openし、reload。
4. LastとRecent先頭から同じordered setを再度開ける。
5. 既存user recentを全削除しない。

### BR-ACC-020 Sidebar cleanup

1. Quick Searchが存在しない。
2. Today/7d/30d/This yearが存在しない。
3. Foldersは初期展開。
4. collapse後にgallery layoutが壊れない。
5. expandで同じbucket状態へ戻る。

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

### BR-ACC-040 Seen

1. 未閲覧fixtureでUnseen dots default OFF、dot 0。
2. ONでunseen数と同じdot数。
3. viewport scrollだけではSeen数が変わらない。
4. 1枚clickでその1枚だけdot消失。
5. keyboard/modal navigationでも対象だけSeen。
6. OFFでdot 0だがSeen mapは意味上同じ。
7. ONへ戻して残りunseenだけdot。

### BR-ACC-050 Search/date/folder

- `cat, night` が両token AND。
- filenameまたはPNG promptでhit。
- negative-only textはhitしない。
- date From/ToがCreatedのinclusive day。
- hidden folderがserver resultから消える。
- stale requestが新queryを上書きしない。

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
- Modal Previous/Nextが端でwrap。
- Closeでcurrent画像をgalleryへreveal。
- center click chrome、double-click metadata、swipe threshold。

### BR-ACC-075 Right preview / tabs

1. Panel default 320px、resizeは240〜900へclampし、reload後にwidthを復元。
2. 768px以下でpanelを非表示。
3. empty時にselection案内。
4. single imageで+1/-1、Open、Enhance、Details。
5. DetailsはPath/Created/Modified/Prompt/Negativeだけで、dimensions/size/Open Tabを表示しない。
6. multi-selectionでcount、Favorite +1、Open、Enhance、Recycleと結果message。
7. Last tab close後、visible RestoreはなくてもCtrl/Cmd+Shift+Tで復元。
8. Pinはreload後の完全tab復元やclose防止をしない。

### BR-ACC-080 Delete

Disposable source copyだけを使用。

1. Cancelでfile/index/selection不変。
2. 3枚のmiddle削除で元の次へ。
3. last削除で前へ。
4. only削除でmodal close。
5. filtered subsetでもそのorderのneighbor。
6. page未load境界をまたぐ削除でもfull query orderの直隣。現行Browserはこのcaseが未解決なので改善acceptanceとして追跡。
7. project root、active index外、unsupportedを拒否。
8. Recycle failureでindexを消さない。
9. hard delete fallbackなし。
10. Enhanced output deleteでsource不変。

### BR-ACC-090 Enhancement isolation

Given: disposable fixtureと利用可能なadapter。Backend/model依存を避けるcontract testでは`sharp-test`を明示する。ncnnを使うならavailabilityを先に確認し、large threshold未満のfixtureを使う。

1. jobs/isolation snapshotを取る。
2. scan、search、100枚scroll、single/double click、modal 20 move。
3. job count、enqueue count、worker-start countが増えない。
4. 利用可能adapterに対する明示Enhanceだけが202 jobを作る。Unavailable backendの503とlarge guardの409は仕様通りの別結果。
5. cancel/retry/output deleteがsourceを変更しない。

### BR-ACC-100 Persistence

- Favorite/View/Seen/Scrollのdebounce後reloadでbrowser-local値を復元。
- pending unmount flushはbrowser localStorageを保証対象にし、shared Favoriteはdebounce/PUT完了を待って別確認。
- malformed viewはapp crashせずdefault/normalization。
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

改善版/WPFのtarget:

- keyboardのみで主要flowを完了。
- dialog close後にfocusを戻す。
- card/row/folderへAutomation/semantic roleを付ける。

現行Browserはclickable card/folder rowにrole/tabIndexがないため、keyboard-only targetをまだ満たさない。Current Browserの合格条件として過大に扱わない。

### BR-ACC-120 Console

Landing → scan → viewer → filters → zoom → preview → modal → settingsのread-only smokeでconsole error/warning 0を目標とする。既知の意図的warningがある場合は要件IDと理由を記録する。

## 22. 現行の既知制約・曖昧点

次は「現行挙動の記録」であり、そのまま再現するMUSTではない。

1. Favorite/Unrated/Enhancedはclient filterで、header totalとvisible数がずれる。
2. Original gridはmasonryではない。
3. Show selectedはisolate filterではない。
4. Folders collapseはreload persistenceなし。
5. Pinはclose防止/完全restoreではない。
6. Restore controlはclosed stackが空でも見える場合がある。
7. Grid background clickは一部inner canvas空白でselection clearしない。
8. Alt+wheelでもGrid zoomする実装分岐があるが、documented shortcutではない。
9. Mobile metadata toggleがdead controlになり得る。
10. Modal sparse orderで未fetchの直隣をDelete candidateから飛ばす可能性がある。
11. Favorite filter中のfavorite変更後、nonmatching modal itemが一時残る場合がある。
12. Settingsにbinding conflict検出、Save/Cancel、reset defaultがない。
13. View/settingsの全field schema/range validationがない。
14. Favorite primary key不在でbackupだけある場合のrestoreに穴がある。
15. Delete後にFavorite/Seen/pin/enhance history orphanが残る。
16. scan cancel/global mutexがない。
17. client disconnectでserver scanを止めない。
18. cold processのmerged cache/tagsがactive scan外のhistorical rootを含み得る。
19. Same-name overwriteをincremental signatureで見逃す可能性があり、full scanが回復経路。
20. queued enhancementはrestart直後に自動resumeしない。
21. Comfy cancelはprompt ID取得前だとglobal interruptの曖昧性がある。
22. shared JSONにcross-process transaction/revisionがない。
23. API errorにlocal path/internal detailが含まれ得る。
24. `/api/image` と `/api/open` は既存supported absolute pathならactive index外でも許す。
25. `/api/scan` はcaller指定absolute directoryを走査し、HTTP layerの認証もない。
26. unknown enhancement preset IDは明示rejectせず先頭presetへfallbackする。
27. ComfyUIのtimeout既定0は無期限待機を意味する。
28. direct component testが少なく、E2EはLanding/recent中心。
29. 通常launcherのport 3000 listenerが`::`で、loopback-onlyを保証していない。
30. Sparse resultのrange/keyboard/Delete neighborはloaded subsetに制限される。
31. Current result外のPreview tab clickはmodalを開かない。
32. Favorite unmount flushはbrowser localStorageだけで、pending shared PUTを完了させない。
33. malformed Favorite PUTはshared mapを空へ全置換し得る。
34. thumb/display/enhance output disk cacheにquota/LRU evictionがない。
35. Delete routeのRecycle Bin/guardを直接検証するunit testがない。

## 23. Source map

基準commitでの主な実装証拠。行番号は将来の編集でずれるため、pathとsymbolを優先する。

| Requirement area | Source |
| --- | --- |
| Runtime/launcher | `package.json`, `scripts/prod_launcher.js`, `scripts/serve_with_parent_watch.js` |
| Types/default keys | `src/lib/types.ts`, `src/lib/imageFormats.ts` |
| Main state/persistence | `src/store/ImageContext.tsx` |
| Landing/header/bulk/global keys | `src/app/page.tsx` |
| Sidebar/filter/folders | `src/components/Sidebar.tsx` |
| Search chips/suggestions | `src/components/SearchBar.tsx` |
| Gallery/zoom/virtualization | `src/components/ImageGrid.tsx` |
| Right preview | `src/components/RightPreviewPanel.tsx` |
| Bottom tabs | `src/components/BottomPreviewTabs.tsx` |
| Modal/Delete neighbor | `src/components/ImageModal.tsx` |
| Settings | `src/components/SettingsModal.tsx` |
| Enhance UI | `src/components/EnhanceQueuePanel.tsx` |
| Search/sort/index/folders | `src/lib/indexer.ts` |
| PNG parse | `src/lib/pngParser.ts` |
| Thumbnail cache | `src/lib/thumbnailCache.ts` |
| Client image cache | `src/lib/clientImageCache.ts` |
| Warmup batching | `src/lib/thumbnailWarmupBatcher.ts` |
| Favorite/Unseen helpers | `src/lib/browserUiPreferences.ts` |
| Modal nav/zoom helpers | `src/lib/modalNavigation.ts`, `src/lib/modalZoom.ts` |
| Selection/key guards | `src/lib/viewerUi.ts`, `src/lib/imageListState.ts` |
| Folder set/recent | `src/lib/pathSet.ts`, `src/lib/recentFolders.ts` |
| APIs | `src/app/api/**/route.ts` |
| Enhance lifecycle | `src/lib/enhance/**` |
| Responsive/layout | `src/app/globals.css` |

### 23.1 Test map

| Contract | Test evidence |
| --- | --- |
| Favorite levels/Unseen | `src/lib/browserUiPreferences.test.ts`, `src/components/Sidebar.test.tsx`, `src/components/SettingsModal.test.tsx` |
| Favorite merge/view migration | `src/store/ImageContext.test.tsx` |
| Modal order/swipe | `src/lib/modalNavigation.test.ts` |
| Modal zoom | `src/lib/modalZoom.test.ts` |
| Gallery arrow math | `src/lib/viewerUi.test.ts` |
| Date | `src/lib/dateFilter.test.ts` |
| Sparse Delete list | `src/lib/imageListState.test.ts` |
| Index/incremental/sort | `src/lib/indexer.test.ts` |
| Scan progress | `src/lib/scanProgress.test.ts` |
| Thumb/cache | `src/lib/thumbnailCache.test.ts`, `src/lib/clientImageCache.test.ts`, `src/lib/thumbnailWarmupBatcher.test.ts` |
| Folder/recent | `src/lib/pathSet.test.ts`, `src/lib/recentFolders.test.ts` |
| Enhancement isolation/store/queue | `src/lib/enhance/jobStore.test.ts`, `src/lib/enhance/queue.test.ts` |
| Landing recent E2E | `e2e/home.spec.ts` |

`src/lib/imageListState.test.ts` はsparse list helperを検証するが、`/api/delete` routeのRecycle Bin呼び出し、project-root/index/type guardを直接testしていない。Delete safety test追加はP0改善候補。

## 24. Change control

この仕様を変える時は次を同じchangeで行う。

1. 変更する要件IDと理由をissueへ記録。
2. Browser implementationとfocused testを更新。
3. port 3000 production runtimeで該当flowを確認。
4. user state/cache削除なしのreload persistenceを確認。
5. [browser-to-wpf-parity-plan.md](./browser-to-wpf-parity-plan.md) のWPF分類と受入条件を同期。
6. Delete、Enhancement isolation、shared state互換へ影響する場合は専用safety testを追加。

この文書だけを見て実装するAIは、CURRENT LIMITATIONを意図せず「正しい仕様」として複製してはならない。MUST、受入試験、Source mapを一組として扱う。
