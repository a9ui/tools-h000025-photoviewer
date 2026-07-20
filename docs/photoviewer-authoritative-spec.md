# PhotoViewer 統合正本仕様 — Browser / WPF

文書状態: **Normative / implementation-ready / local-main baseline**

監査基準日: 2026-07-20 JST

実装基準: このマシンの `refs/heads/main` に採用されたBrowser/WPF基盤
`792716f9863dd145e028648405c29c340f7a4336`、repository hardening
`4c81cca3efc80363568d0d9af35297ff3285b48c`、WPF gallery zoom/geometry anchor
`e371b482af44e0428d9fe0d5217b236801f29cff`、shared-state latency `5ae1e00`、表示中asset open/容量表示
`a1d83c8`、WPF focus assertion `dbad550`、Modal button Enter isolation `452ac02`、Browser shared-writer FIFO fairness `0802f95`を含むdescendant。`origin/main` は基準ではない。

現在の実装状況と証拠は [current-implementation-truth.md](./current-implementation-truth.md) を参照する。

## 0. この文書の役割

この文書は、別のAIまたは開発者がチャット履歴を読まずに、PhotoViewerのBrowser版とWPF版を同じ製品意味で再実装するための統合正本である。見た目だけでなく、表示条件、pointer/keyboard、右クリック、状態遷移、保存先、Browser/WPF共有、失敗・空・大量画像・再起動、性能、安全境界、受入試験を契約に含める。

規範語:

- **MUST**: 同等製品と認めるため必須。
- **SHOULD**: 原則実装する。省略時は理由と代替受入証拠が必要。
- **MAY**: surface固有の裁量。
- **CURRENT LIMITATION**: 現行実装の制約。再現必須ではない。
- **PENDING / 実装確認待ち**: 採用前。現在の正規挙動として実装しない、実装済みと報告しない。
- **NON-GOAL**: 製品対象外。勝手に正規機能へ加えない。

仕様衝突時の優先順位:

1. この文書の安全契約とMUST
2. `docs/current-implementation-truth.md` の採用状態
3. 基準commitの実コード
4. 基準commitのfocused test/verifier
5. `docs/browser-feature-contract.md` と `docs/wpf-product-spec.md` のsurface詳細
6. 過去README、旧recap、mockup、未採用branch/worktree

未採用worktree、ローカルbranch、mockup、予定表は、`main`へ採用されfocused testが付くまで正本を変更しない。

## 1. 製品定義と絶対条件

PhotoViewerは、Windows上のローカル画像folderを走査し、大量の生成画像・写真を高速に検索、比較、評価、整理するlocal-first viewerである。Stable Diffusion PNG metadataを利用できるが、画像生成器、upscaler、cloud galleryではない。

Browser版はNext.jsによるlocal desktop-like surface、WPF版は.NET 8 native desktop surfaceである。DOM、CSS、HTTPの一致ではなく、ユーザーが観測する結果、状態の意味、安全境界を一致させる。

両surfaceのMUST:

1. Favoriteはabsolute pathごとの整数Lv0〜5。Lv1〜5 filterは独立exact-matchで、未選択はAll。
2. Seenは明示閲覧時だけadditiveに増える。Unseen dotsは表示設定だけで、既定OFF。
3. source Delete成功後は、削除前のfiltered/sorted order上のnext、なければpreviousへ継続する。
4. source DeleteはWindows Recycle Binだけを使い、hard deleteへfallbackしない。
5. gallery zoomは画像cardだけを変え、sidebar、header、font、right panel、List rowを拡縮しない。
6. zoom前後でユーザーが見ていた基準画像のviewport offsetを可能な限り保つ。
7. scan、thumbnail、preview、modal navigation、tab hoverはEnhancement jobを作らずworkerを開始しない。
8. source画像を編集・上書きしない。Enhancement outputは別managed pathへ保存する。
9. 大量画像をsilent truncateしない。visual realizationの上限とcatalog件数の上限を混同しない。
10. 既存state/cache/shared JSONを速度改善やmigrationのために削除しない。

明示的に復活禁止のUI:

- Quick Search
- Today / 7d / 30d / This year の相対日付preset
- Favorite threshold (`Lv N+`)

NON-GOAL:

- account、cloud sync、multi-user collaboration、remote公開
- permanent source delete
- editable user tag database
- passive browsingからの自動AI処理
- deployment。Vercel配備は別途明示承認が必要
- WPFへのBrowser server内蔵

## 2. Runtimeとtrust boundary

### 2.1 Browser

- Next.js 16.2.10、React 19.2.7、Node.js 20.9以上、pnpm 11以上。
- 通常入口はproject rootの `start_viewer.bat` → `scripts/prod_launcher.js`。
- production serverは `127.0.0.1` のみへbindする。認証、CSRF、Origin validationを持たないため、信頼済みlocal machineの外へ公開しない。
- launcherは完全path tokenとprocess treeで所有権を証明できるprocessだけを扱う。port番号、`node.exe`名、root文字列の部分一致だけで停止しない。
- explicit busy portは奪わず失敗する。通常起動は3000〜3999の空portを選ぶ。
- source revision、dirty state、build ID、build time、host、port、start time、process IDをruntime provenanceとして渡す。
- `GET /api/runtime` はno-storeでprovenanceを返すが、project root、token、環境全体は返さない。
- ComfyUI autostartは既定OFF。`PVU_COMFY_AUTOSTART=1` の明示時だけ起動経路を使える。

### 2.2 WPF

- .NET 8 Windows WPF single process。
- projectは `local-native/PhotoViewer.Wpf/PhotoViewer.Wpf.csproj`。
- 通常入口は `start_wpf.bat`。
- launcherはrepo root、project/target path、git revision、WPF source fingerprint、Release exe SHA-256のprovenanceが一致する時だけ既存exeを直接起動する。
- missing/unproven/invalid/wrong-worktree/wrong-revision/source drift/target改変はRelease buildへ戻る。build/provenance失敗時に旧exeを起動しない。
- Browser serverやport、既存WPF processを開始・停止・killしない。
- Browser Node runtimeなしで通常閲覧できる。明示Enhancement actionだけはloopback Browser APIへ委譲できる。

### 2.3 Privacyと検証process

- diagnostic、screenshot、reportへsecret、cookie、`.env`、raw state/cache、private image pathを載せない。
- runtime diagnostic UIは必要最小限のversion/build/runtime/count/safety情報だけを表示する。
- verifierはTEMP fixtureとoverride pathを使い、ユーザーFavorite/Seen/Recent/state/cache/sourceを変更しない。
- ユーザー所有のport/processを検証のために再起動しない。Browserは `scripts/verify-browser-runtime.ps1`、WPFは `scripts/check-wpf-launch-target.ps1 -Json` のread-only経路を使う。

### 2.4 WinFormsのFROZEN境界

`local-native/PhotoViewer.Native/**` のWinForms版は削除もarchive化もしないが、2026-07-19からユーザーが明示解除するまで **FROZEN** とする。これはimplemented、partial、pending、blockedの進捗状態とは別の運用状態である。

- 新機能、Browser追随、WPF追随、通常の製品gate、日常的なlauncher/runtime受入の対象にしない。
- 保守を許すのは、重大なデータ破損または起動不能だけである。見栄え、機能差、通常の不具合は解除理由にならない。
- 保守する場合もBrowser/WPF共有データとの互換を壊してはならず、WinForms固有SQLiteをBrowser/WPFの正本に昇格させない。
- `start_winforms.bat` と `scripts/start-local-native.ps1`、既存source、既存verification文書は残す。存在することを「現行製品gateを満たす」「新要件を実装済み」と解釈しない。
- 現行`NativeStateBridge`は `.cache/favorites.json`、`seen.json`、`recent-folders.json`を読み書きするが、Browser/WPFのcross-runtime verifierにWinForms actorは含まれず、同時writer用lockも静的監査では確認できない。lost updateまたは破損が再現した場合だけFROZEN保守条件に該当する。
- 再開条件はユーザーの明示的なFROZEN解除だけである。branch、issue、古いtask、launcherの存在は解除にならない。

## 3. 共通データmodelと所有権

### 3.1 Image identity

画像identityはcanonical absolute path。case-insensitiveなWindows path比較を行い、同じpathが複数rootに含まれても1画像とする。UI、selection、Favorite、Seen、tab、modal、Delete guardは同じidentityを使う。

Browser `ImageFile` の最小形:

```ts
type ImageFile = {
  id: string;
  filename: string;
  absolutePath: string;
  fileUrl: string;
  displayUrl: string;
  fullUrl: string;
  metadata: SDMetadata | null;
  createdAt: number;
  mtime: number;
  isFavorite?: boolean;
};
```

Browser対応形式はPNG/JPG/JPEG/WebP/AVIF/GIF。WPFはこれにBMP/TIF/TIFFも加える。拡張子はcase-insensitive。decode失敗は該当画像だけをplaceholderにし、catalogを落とさない。

PNG metadataは最初のIDATより前にある最初のkeyword `parameters` のtEXtだけを採用する。Prompt、Negative prompt、generation settingsを分離する。最初のparameters chunkが空でも後続へfall throughしない。検索対象はfilenameとPromptで、absolute path、negative prompt、settings全文は標準検索対象外。

### 3.2 共有store

linked worktreeで実行しても `.git/commondir` から通常main checkoutを解決し、次のshared storeを分岐させない。

| Data | Path | Schema / owner |
| --- | --- | --- |
| Favorite | `.cache/favorites.json` | `Record<absolutePath, 1..5>`。Lv0はentryなし。Browser/WPF read-write |
| Seen | `.cache/seen.json` | additive `Record<absolutePath, true>`。Browser/WPF read-write |
| Recent | `.cache/recent-folders.json` | version 1、last set + 最大12 recent sets。Browser/WPF read-write |
| Search History | `.cache/search-history.json` | version 1、MRU `entries: string[]` 最大50件 + `updatedAtUtc`。Browser/WPF read-write、WinFormsは非参加 |
| Settings | `.cache/settings.json` | Browser app settings/key bindings。Browser read-write |
| Scan index | `.cache/index_<hash>.json` | Browser version 1 index |
| Folder signature | `.cache/folders_<hash>.json` | Browser version 2 signature |
| Thumbnail/display | `.cache/thumbs/**`, `.cache/display/**` | Browser derived cache。WPFはcompatible thumbnailをread-only利用 |
| Enhancement jobs | `.cache/enhance/jobs.json` | Browser engine read-write。WPFはread-only参照、変更はBrowser API経由 |
| Enhancement outputs | `.cache/enhance/outputs/**` | Browser engine managed output |

Favorite/settings/recent/seen writeはtarget別create-new lock、bounded retry、30秒stale recovery、lock内latest-disk再読、temp+atomic replaceを使う。malformed/future documentは空で上書きせず、409またはrecoverable refusalにする。unknown fieldと他writerの無関係entryを保持する。

Favoriteはbase→desiredのdirty pathだけをlatest diskへthree-way mergeする。Seenはtrue unionで削除・false化しない。Recentは各writerのlatest setをhistoryへ加算し、lastFolderSetは最後に正常lockを取得したwriterを採用する。

Search Historyはraw queryとcomma正規化後queryの両方を32,768 UTF-16 code unit以下に制限し、自己保存したentryを次回readでoversized扱いする状態を作らない。comma tokenはBrowser/.NET共通の明示trim集合（Unicode White_Space + U+FEFF BOM）でtrimし、emptyを除いて`, `で再結合する。identityはNFKC後にBrowser/.NET共通のcode-point単位lowercaseを行い、U+0130は`i` + combining dotへ明示展開する。同一identityの旧entryを除いてcommit queryをMRU先頭へ置き、最大50件へ切る。既存documentが50件を超える場合は拒否せず、read時に先頭50件へcanonicalizeする。`updatedAtUtc`はread時optional、正常mutation時は必ずUTC ISO文字列を書く。個別DeleteとClear allを持つ。unknown root fieldを保持し、missingはempty、malformed/future versionは表示をemptyにして既存bytesへのwriteを拒否する。Browser/WPFは同じ`<target>.lock`、lock内再読、30秒stale recovery、temp + atomic replaceを使い、timeout/Busy時はcurrent queryと既存JSONを壊さない。Browserは同一process・同一targetのwriterをFIFOへ並べるが、各turnで共有file lockを省略しない。FIFO待ちはcross-process lock timeoutへ算入せず、高負荷時の同一process starvationを防ぐ。

### 3.3 Browser localStorage

| Key | Meaning |
| --- | --- |
| `pvu_favorites`, `pvu_favorites_backup` | path→Lv1..5 local mirror/backup |
| `pvu_favorites_pending` | exact shared-write journal。clearもdirty pathとして表す |
| `pvu_favorites_shared_migration_v1` | one-time shared import marker |
| `pvu_view` | view/panel/sort/date/folder/unseen/queue settings |
| `pvu_preview_tabs`, `pvu_pinned_tabs` | ordered tabs/active/pins |
| `pvu_fav_only`, `pvu_unfav_only`, `pvu_fav_levels` | Favorite filters |
| `pvu_enhanced_only` | Enhanced-only filter |
| `pvu_scroll_memory` | view key→scrollTop、最大80 |
| `pvu_seen_images` | additive Seen local mirror |
| `pvu_last_dir_set`, `pvu_recent_dirs` | Browser folder memory |
| `pvu_enhance_settings` | explicit Enhancement settings |

`pvu_view` はviewMode、thumbSize、aspectMode、displayStyle、sidebar/right panel、sort/random、folder sort、modal edge ratio、modal filmstrip、queue、manual dates、hidden folders、Unseen dots、Folders collapseをfield-localにnormalizeする。`modalFilmstripOpen`は既定trueで、falseをModal close/reopenとreload越しに保持する。現行Browser mainのthumbSizeは20〜600、default 200、20px step、columnsは常に0/auto。600 endpointだけはavailable widthに関係なく1列へ解決する。旧保存値は20〜600へclampする。

Favoriteは即時local mirror + 300ms shared debounce、Seenは即時local mirror + 900ms additive shared debounce、viewは300ms debounce。pagehide/hidden/unmountのkeepaliveはUTF-8 60KiB以下かつwrite非実行中だけ試し、未確認・oversize・失敗はjournalを次回hydrationへ残す。

### 3.4 WPF local state

既定pathは `%LOCALAPPDATA%\PhotoViewer.Wpf\state.json`。metadata indexは同じowner directory配下の `metadata-index-v1/<folder-set-hash>.pvmi`。test override以外で共有storeへ混ぜない。

ViewerState v2はLastFolder(Set)、SearchQuery、SelectedPath、CardWidth、right panel、style/aspect、sort/random seed、manual date、Favorite filters、Unseen dots、ConfirmBeforeDelete、Folders collapse/hidden/selection、preview tabs/pins、editable KeyBindings、unknown extension fieldsを保存する。shared Search Historyは別ownerの`.cache/search-history.json`であり、ViewerStateへ埋め込まない。

state writeは300ms debounce、target lock、latest disk merge、unknown field保持、atomic replace。window closeは最終viewer stateを1回flushするが、source、Favorite、Seen、Recent、Enhancementを副作用で書き直さない。malformed/future/contended stateは既存bytesを保持してcloseできる。

### 3.5 保存しない状態

Browserはcurrent search query、current selection/primary、active modal、画像ごとのOriginal/Enhanced表示選択をreload restoreしない。disk上のSearch History一覧と`modalFilmstripOpen`はこの非保存対象とは別である。WPFはSearchQuery/SelectedPathをViewerStateへ保存し、Search History一覧はshared fileへ分離するが、reloadでModalを自動openしない。両surfaceとも一時zoom/pan/flip、open dialog、in-flight request、hover popupを保存しない。

## 4. 共通状態機械

Top-level:

```text
Landing --Open/Scan--> Loading/Scanning --complete--> Viewer
   ^                         | cancel/fatal             |
   |                         +--------------------------+
   +---------------- logo / Change folder -------------+
```

- Landingはfolder-set draftを編集する。Add/paste/removeだけではsourceを変更しない。
- Loading/Scanningはcurrent generationだけを所有する。cancel/stale completionは新runや既存catalogを上書きしない。
- Viewerはsidebar、gallery、right preview、tabsを持ち、Modal/Settings/Delete/Enhancement statusが上に重なる。
- Overlay中は背面galleryのglobal shortcutを漏らさない。Escapeは最上位overlayを閉じ、可能ならopenerへfocusを戻す。
- 新catalogのscan成功時だけ旧catalogのselection、modal order、preview tabs/pins等をreconcile/clearする。失敗・cancel・same-catalog refreshは成功済みUI contextを破壊しない。

## 5. Landing / scan / restart

### 5.1 表示と操作

| Element | Browser | WPF | Result |
| --- | --- | --- | --- |
| Add folder | Windows picker | native picker | canonical existing folderをdraft末尾へ追加、case-insensitive dedupe |
| Paste paths | 1行1absolute path、Ctrl/Cmd+Enter | 1行1absolute path | trim/blank除去、順序保持、scanはまだ開始しない |
| Remove row | button | button | draftからのみ除去。source folderは削除しない |
| Open folder set | click。Shift-clickはfull verify | click | empty draftでは開始不可 |
| Last/recent | last + 最大12 | last + 最大12 | shared malformed時は上書きせずstatus |
| Cancel scan | scan中だけ | pre-publication中だけ | current generation cancel、draft保持、遅延event無視 |
| Folder drag-in | Browserは正規契約なし | Landing/Viewerでnative FileDrop | WPFはfolderだけ受理しcopy/move/deleteしない |

### 5.2 Scan

Browserはroot直下fileと直下folderごとのrecursive targetを走査し、symlinkをfollowしない。mtime+sizeが同じfileはmetadata cacheを再利用し、changed/newだけparse、staleを除去する。rootごとのfailureはcached pathを保持でき、全root失敗だけfatal。SSE progressはprogress/complete/errorを返し、complete時にopaque `indexToken`を発行する。

WPFは各rootをrecursive enumerationし、reparse pointを追跡せずlexical/canonical root外へ出ない。日本語、emoji、空白、apostrophe、long pathをidentityのまま扱う。一部root/fileのmissing/permission/decode failureはsurvivor catalogをpublishし、skipとrecoveryをstatusに残す。

### 5.3 Sessionと再起動

Browser indexTokenは最大8 session、last-useから30分TTLのprocess memory snapshot。token付きrequestはそのsnapshotだけを使い、別windowのactive fallbackへescapeしない。

- search requestの失効は410 `Session expired`。明示 `Rescan folder set` でfresh tokenを得る手動回復を残す。
- thumbnail/display/full image requestの明示tokenが失効した場合、`CachedImage`は同一requestを無限retryせず1回だけcontextへ通知する。現在のfolder setを`preserveViewer: true`で自動scanし、Viewer/Gallery、selection、Modal、tabs、Favorite/Seenを保持したままfresh tokenへ差し替える。
- 自動scan成功後はtoken changeで画像requestを再発行する。失敗時はViewerを維持して`Automatic viewer session refresh failed`を表示し、同じfolder setのmanual Rescanを残す。
- 同じ失効generationからの複数画像410は1回へcoalesceする。透明placeholderまたは既存thumbnail fallbackを使い、expired URLをnetwork loopさせない。

Browser再起動後はlast/recent folder set、view、Favorite/Seen local mirror、tabs/pinsを契約どおり復元するが、active modalは開かない。WPF再起動後はViewerState v2、shared Favorite/Seen/Recent、compatible thumbnail、persistent metadata indexを復元する。missing sourceはUI参照だけをreconcileし、Favorite/Seen/Enhancement historyを勝手に消さない。

## 6. Browser画面・部品契約

### 6.1 Header

Viewer中にmenu/sidebar、logo/landing return、Refresh、Search、count、right preview toggle、Settingsを表示する。countはindexed/totalと、client filter後のloaded/shownを混同しない。Refreshはsame folder setを再scanし、Favorite/Seen/Enhancementをresetしない。

### 6.2 Sidebar

desktopは固定240pxでgallery zoomから独立。mobileは`max-width:768px`で`min(88vw,360px)`のdrawer、backdrop、dialog semantics、focus trap、Escape/outside close、opener focus returnを持つ。

上からFolders、Favorite/Unrated/Lv1〜5、Enhanced、manual From/To、folder buckets、sort、Grid/List、style、aspect、thumb size、App Settingsを配置する。Foldersは既定展開、collapse stateを `pvu_view.foldersExpanded` に保存する。childはcollapse中Tab対象外。

Folder bucketsはshow/hide、Show all、Hide all、Invert、Show selected、Hide selected、sortを持つ。bucket filterはFavorite/Seenを変更しない。APIは`indexToken`を受理するが、現行`Sidebar.tsx`のfolder取得はtokenを送っていない。複数window/sessionの完全分離契約に対する既知の実装gapであり、再実装ではactive global fallbackに依存せずcurrent tokenを渡す。現行mainをimplementedと評価しない。

### 6.3 Search

comma区切りtokenをtrimしemptyを除く。token間AND、各tokenはfilenameまたはPromptへcase-insensitive substring match。spaceはtoken内文字として扱う。Enter/commaでchip化、Backspaceで末尾編集、drag/touch handle/Alt+Shift+Left/Rightでreorderする。

suggestionは最大8件を表示し、ArrowUp/Down、Enter/Tab、Escape、mouseで操作し、combobox/listbox/option semanticsを公開する。既存Browser契約にある最大12件という記述は現行mainと一致しない。queryはdebounceし、sort/date/folder/index session変更時は旧timer/requestをcancelまたはgenerationで破棄する。

Search入力をfocusまたはclickすると、その時点のshared Search Historyをdiskから再読してRecent searchesを開く。typing中はsuggestionへ切り替える。履歴選択はcurrent queryへのtag追加ではなく、保存されたcomplete queryによるwhole-query replacementである。inputの初回ArrowDownは先頭、初回ArrowUpは末尾へ入り、その後は循環する。履歴row上のArrowUp/Downは実focusを隣接rowへ移し、Enterで適用、Escapeで閉じてinputへfocusを戻す。disk再読中は旧snapshotを表示・Arrow/Enter選択せず、他runtimeで削除されたqueryを不可視に再commitしない。mouse/touch選択、個別Delete、Clear allを持つ。確定したtag/query、履歴選択、入力からfocusが外れた時だけcommitし、debounce中の文字列を履歴化しない。read/write failure、malformed、future versionはlive statusへ出し、current searchを消さない。

### 6.4 Gallery

- GridとList。Gridはfull logical order + visible/overscan virtualization、Listもcurrent full orderを使う。
- Grid aspectはOriginal contain、1:1 cover、2:3 cover。Originalはnatural-height masonryではない。
- click/Spaceはselection、Ctrl/Cmdはtoggle、Shiftはrange。背景clickだけがclear。
- image primary controlのEnterはModalを開く。これは現行mainで実装済み。
- Arrow keysは現在のGrid column数またはListの1行を使いprimary selectionを移動し、reveal/focusする。
- drag-outはURI/text/DownloadURLを設定するが、OS native file copyと同一保証ではない。
- normal click/modal openはSeenを付ける。virtualize/scroll/thumb decodeだけでは付けない。
- query/filter empty、no supported image、request failureを別copyにする。

現行Browser zoomは20〜600px、20px step、reset 200。600はwide/4Kでも1列endpoint、20はbounded cell widthを保った高密度endpoint。Ctrl/Cmd+wheelとCtrl/Cmd `+/-/0` はGrid activeかつModal/Settings/input外だけで処理する。selected cardがviewport内なら最優先anchor、次にpointer直下、最後にviewport center近傍を使い、reflow後scrollをclamp補正する。Sidebar open/closeなどthumbSize以外のcolumn/row geometry変化でも直前anchor pathとviewport offsetを保持する。List modeではSize変更とanchor補正を行わない。

### 6.5 Right preview

desktop default 320px、240〜900px、separator dragとkeyboard Arrow/Home/Endでresizeし `pvu_view` へ保存する。single selectionは画像、filename/path、Favorite、Open、tab、metadata、source Delete、明示Enhanceを表示。multi selectionはcount、Favorite一括、Recycle一括。selectionなしはguidanceだけを表示する。

decodeはimmediate thumbnail/placeholder + cancellable full decode。古いpath/generation completionを捨て、current decode failure時は古い成功bitmapを残さない。

### 6.6 Bottom preview tabs

selectionからopen/activate/pin/close/Close all/Reopen last closed/reorderを行う。最大30、case-insensitive dedupe。order/activeは `pvu_preview_tabs`、pinは `pvu_pinned_tabs`。reload後はcurrent catalogに存在するtabだけを復元し、Modalは開かない。current result外tabは保持するが、その状態でModal openを偽装しない。

### 6.7 Modal

entryはgalleryのfiltered/sorted full orderをsnapshotする。Left/Right/button/edge zone/swipeで移動し、sparse resultは必要pageを解決する。先頭/末尾の扱いは現行Browser helperのclamp/wrap contractに従い、Delete neighborは必ずpre-delete orderで決める。

表示:

- center image、black backdrop、top toolbar、bottom feedback、left/right edge、metadata sidebar
- filtered/sorted Modal orderを横に示すvirtualized filmstrip。manual UI表示中は画像に重ねず専用下段を確保してviewportを縮める。manual UI非表示中の下端hoverでは前面overlayとして出し、離脱時に畳んで画像geometryを動かさない。current imageへ追従し、thumbnail clickで直接移動
- zoom 0.25〜10、wheel/`+/-/0`、pan、horizontal flip
- succeeded managed outputがあればEでOriginal/Enhanced切替
- 現在表示中assetの実体容量を、bytes / 1024²、小数2桁、spaceなしのexact `0.00MB`形式で表示する。Originalはsource file、Enhancedはmanaged output fileのbytesを使い、E切替時に同じresolverで即更新する
- center image single clickでchrome toggle、double clickでmetadata sidebar、empty backdropでclose
- manual chrome表示は操作停止でも消さない。manual非表示ではcursorも隠し、Modal内pointer移動またはkeyboard操作で約900msだけ一時表示して再び隠す。画像遷移、Delete隣接移動、Original/Enhanced切替後もmanual非表示を維持する
- zoom倍率indicatorはModal rootの上端中央へ低contrastで表示し、top toolbar、画像、filmstripと重ねない
- Prompt/Negative/Settings tabsとtext copy
- Delete、Open external、explicit Enhance、Cancel/Retry/output delete

filmstripはviewport + 左右8 item overscanだけをrealizeし、100,000 logical imagesでも全thumbnail DOMを作らない。sparse orderはvisible/overscan rangeと交差するsearch pageだけを取得する。active image変更時はstripを中央へscrollするが、通常navigationでfocusを強制移動しない。toolbarとstrip内Hide、configurable既定`T`で開閉し、`pvu_view.modalFilmstripOpen`へ保存する。filmstrip内ArrowLeft/RightはModal previous/nextを1回だけ実行する。strip上のpointer/click/wheelをcenter chrome toggle、edge click、pan、swipe、wheel zoomへ漏らさない。

fixed keysはEscape close、Left/Right navigation、Favorite increase/decrease、configured Delete/Enhance/flip/zoom/filmstrip、E toggle、Space metadata sidebar、Enter external open。input/overlayでは背面shortcutを抑止する。top toolbar buttonへfocus中のEnter/Spaceはnative activationを優先するが、Arrow/Delete/T/H/F等のModal shortcutは引き続き届く。

Enter external openと容量表示は同じdisplayed-asset resolverを使う。Original表示中はactive-index内source、Enhanced表示中は対応するsucceeded jobのmanaged outputを対象にする。Enhanced job/outputがmissing、source signatureがinvalid/stale、source identityが不一致、output typeがunsupported、またはoutputがlexical/real final-pathのどちらかでmanaged outputs root外ならOriginalへfallbackし、その容量とrecoverable statusを表示する。`POST /api/open`だけが外部appを起動し、`GET`/`HEAD`は容量解決だけで副作用を持たない。active index/session membership、source type/existence、managed ownershipを起動直前にも検証し、shell failureはModalを閉じずRetry可能なstatusへ返す。

### 6.8 Settings / Enhancement queue

SettingsはBehavior、editable key bindings、Runtime/Version diagnosticsを持つ。initial focus、Tab trap、Escape、focus return、loading/error live regionを備える。key conflict、invalid chord、reserved chordをinline表示し、Save成功時だけactive mapへ反映する。

Enhancement queueはjobがある時または明示open時だけ表示し、queued/running/succeeded/failed/canceled、progress、cancel、retry、open output、delete managed outputを扱う。通常閲覧からは開閉・enqueueしない。

### 6.9 Browser right-click / Ctrl+C

現行mainに製品定義済みimage context menuはない。browser既定context menuを正規action surfaceとして扱わない。Ctrl+CはPrompt等のtext copy以外、画像bitmap/file copyとして定義されていない。どちらもSection 14のPENDINGである。

### 6.10 Browser keyboard mapとguard

shared configurable bindingの既定は次。Settingsでcellを選び次のkeydownをdraftへ記録し、normalized conflict中はSaveを無効化する。Resetはdraftだけを既定へ戻し、Save成功まで永続化しない。

| Action | Default |
| --- | --- |
| Modal next / previous | ArrowRight / ArrowLeft |
| Favorite +1 / -1 | F / U |
| Source Recycle | Delete |
| Close Modal | Escape |
| Flip horizontal | H |
| Explicit Enhance | A |
| Zoom in / out / reset | `=` / `-` / `0` |
| Toggle modal filmstrip | T |

`Toggle modal filmstrip`はSettingsのconflict検査対象である。旧settingsで`T`が別actionに使われている場合、その既存bindingを保持し、filmstripだけを未使用の`B`、`G`、`V`、`Y`の順へmigrationする。ModalのOriginal/Enhanced `E`、metadata sidebar `Space`、external open `Enter`、preview tab reopen `Ctrl/Cmd+Shift+T`は固定actionである。filmstrip focus内の物理ArrowLeft/Rightもcontextual fixed navigationである。Grid/ListではArrowとSpaceがselection、Enterがcurrent filtered/sorted orderのprimary imageをModalで開く。Ctrl/Cmd+1〜5はFavorite exact level。

dialog/alertdialog active、input/textarea/select/button/contenteditable focus、別app/browser commandを示すmodifier、interactive pointer操作中はgallery shortcutを通さない。shortcutでactionを実行した時だけdefault browser actionをpreventする。

### 6.11 Browser responsive / touch / accessibility

主要breakpointは`max-width:768px`。Headerはlogo/countを隠してSearchと主要iconを残す。Sidebarは`min(88vw,360px)`のfixed drawer、Right Previewは非表示。Modal metadataは最大62vhのbottom sheet、toolbarはwrap、Settings/Deleteはviewport内、Landing controlsはstackする。

touchはModal swipe/pan、Search chipのpointer handle reorderを持つ。pinch zoomとgallery touch multi-selectは現行契約外。handle外の縦gestureはpage scrollへ渡す。

- global `:focus-visible`は2px accent outline。
- Modal/Settings/sidebar/bottom sheetはrole、accessible name、initial focus、Tab trap、Escape/backdrop close、opener focus returnを持つ。
- source Deleteは`alertdialog`、scanはstatus/progressbar/live value、Favorite feedbackとreorder/copy/errorは適切なlive regionを持つ。
- folder collapseは`aria-expanded`、Search suggestionはcombobox/listbox/option + active descendant、imageはalt、icon-only controlは`aria-label`を持つ。Search Historyは別のnamed regionとbutton rowsで、inputの`aria-expanded`/`aria-activedescendant`はsuggestionだけを表す。
- filmstripはhorizontal listbox/option、current 1件の`aria-current`/`aria-selected`、logical全件中の`aria-posinset`/`aria-setsize`を持つ。toolbar toggleは`aria-expanded`、`aria-controls`、`aria-keyshortcuts`を公開し、strip内Hideは`aria-controls`と`aria-keyshortcuts`を公開する。
- Right Preview splitterはseparator role、orientation、min/max/current value、Arrow/Home/End操作を持つ。
- iconに文字記号を代用せず、長いruntime ID/path/copyはdesktop/mobileでpanel外へoverflowしない。

## 7. WPF画面・部品契約

### 7.1 Window / shell

default 1280×820、minimum 900×560、custom 52px header、40px bottom tab bar。bodyはSidebar / Gallery / keyboard-resizable splitter / Right preview。maximizeはcurrent monitorのwork areaを使い、mixed DPIをDIPへ変換する。window boundsはcross-launch保存しない。

Headerはsidebar、logo/landing、Refresh、Search、count、Settings/right preview、window controls。SidebarはFavorite、manual date、Folders、sort、Grid/List、style、aspect、zoom、Settings。Quick Searchとrelative date presetを置かない。

### 7.2 Catalog / performance surface

`_allTiles`は全supported imageの軽量catalog、`_tiles`はcurrent filter/sort後の全order。Grid/List ItemsSourceは先頭windowではなく `_tiles` 全件を参照する。Grid `VirtualizingWrapPanel` はfull extentを計算し、visible + 2 overscan rowsだけをrealizeする。Listはrecycling `VirtualizingStackPanel`。

軽量path catalogを先にpublishし、dimension/PNG Prompt metadataは250ms以上遅延したbackground streamで追加する。10,000件以上はmetadata workerを最大2へ抑え、visible thumbnailを優先する。persistent metadata indexはcomplete current-generation snapshotだけatomic commitし、warm restartでunchanged entryを再読込しない。

### 7.3 Selection / gallery

click single、Ctrl toggle、Shift range、Ctrl+Shift additive range。Ctrl+Aはfiltered result全path、Ctrl+Shift+Aはclear。canonical selectionはpath setで保持し、100,000件のWPF SelectedItemsをmaterializeしない。Grid/List/preview/tab/modalは同じprimary pathを共有する。

Grid/List切替、Standard/Compact/Poster、Original/1:1/2:3を持つ。現行zoomは20〜600、20px step、reset 200。600 endpointはavailable widthに関係なく正確な1列へ固定する。Ctrl+wheel、Ctrl `+/-/0`。zoom前にvisible selectionを優先したcanonical full path+viewport offsetをcaptureし、selectionなしではviewport centerを使う。zoom、Sidebar、right panel、window resize、DPI change後に同じpath/offsetをclamp復元し、sidebar/header/font/right panel/List rowを拡縮しない。List modeではgallery zoomを拒否してrecycling virtualizationを維持する。

Grid/List itemはdouble-clickとEnterのどちらでもModalを開く。Enterはcurrent filtered/sorted orderのprimary imageを使い、Modal navigation後に閉じた時も現在のprimary Grid/List itemへfocusを戻す。Search、Date、Settings、Delete、Modal native input、LandingのEnterはgalleryへ漏らさない。Prompt chip等のbuttonへfocus中のEnter/Spaceはnative activationを優先する。

### 7.4 Sidebar / Search / filter

Searchは150ms debounce、comma token AND、filename+indexed Prompt substring、background immutable snapshot、generation/cancelでlatest-only apply。FavoriteはFavorites OFFならlevel無視、ON+empty levelsならAll、ON+levelsならexact OR。UnratedはLv0でFavoritesと排他。

Search入力のfocus/clickでshared Search Historyをbackground threadから再読し、popupへ最大50件を表示する。UI threadでdisk readや最大2秒のlock待ちをしない。履歴選択はwhole-query replacement、ArrowUp/DownでListBox selection、Enterで適用、Escapeで閉じる。個別DeleteとClear allを持ち、selection後にSearchへfocusを戻してもpopupを即再表示しない。loading、protected、Busy、failureをlive announcement/statusへ出す。WPFのcurrent SearchQuery保存とshared Search History一覧は別ownerである。

manual Created/Birth From/Toだけを持つ。Foldersはbucket show/hide、Show/Hide all、Invert、Ctrl/Shift selection、Show/Hide selected、collapse persistence。SortはModified new/old、Created new/old、Name、Random+seed。

### 7.5 Right preview / tabs

Right preview default 340px、240〜900px、mouse/keyboard resize、open/width persistence。single selectionはimage、path、Favorite、Open external、Show in folder、Open tab、metadata、Recycle。multi selectionはcount、Favorite/Recycle。noneはguidanceだけ。

Preview tabsはopen/activate/pin/close/Close all/Reopen、Ctrl+Shift+T、drag/Alt+Shift+Arrow reorder、middle-click close、hover preview、最大30、reload persistenceを持つ。sourceが外部消失した時はUI参照だけを除き、Favorite/Seen/Enhancement historyは保持する。

### 7.6 Modal

filtered/sorted full orderを使い、Left/Right/button/left-right 28% edge zoneでnavigationし、端でwrapする。center 44%、single click manual chrome、double click metadata sidebar、black backdrop close。zoom 0.25〜10、wheel/key、pan、flip、swipe、650ms feedback。

manual chrome表示は操作停止でも出たままとする。manual非表示ではcursorも隠し、pointer/keyで約900msだけ一時表示して戻す。非表示状態はnavigation、Delete隣接移動、Original/Enhanced切替後も維持する。manual Filmstripは画像外の専用bottom row、非表示中の下端hover Filmstripは画像geometryを変えない前面overlayとし、`T`/toolbarで開閉してViewerStateへ保存する。倍率はModal root上端の低contrast indicatorへ表示する。Modal top buttonへfocus中でもArrow/Delete/T/H/F等を処理し、Enter/Spaceはbutton native activation、TextBox等は隔離する。Prompt chip等のbutton Enter/Spaceもnative clickを1回だけ実行し、gallery/modal openへ漏らさない。

Prompt/Negative/Settings tabs、metadata copy、Prompt chip→Searchを持つ。Original/Enhanced toggleは既存succeeded managed outputだけ。現在表示中assetの実体容量をbytes / 1024²、小数2桁、spaceなしのexact `0.00MB`形式で表示し、E切替で即更新する。Enhancedがmissing/stale/invalid signature/ownership外ならOriginal容量とrecoverable statusへfallbackする。explicit AI x2だけがBrowser loopback APIへcreateし、WPF自身はworker/job storeを所有しない。

`Open externally` と `Show in folder` はactive root/catalog/type/existence/canonical guardを実行し、shell文字列結合を使わない。Modal `Open externally` / Enterは現在表示中Original/Enhancedと容量表示に使った同じresolverを使い、managed outputにはsource signatureとcanonical ownership guardも適用する。missing/stale/invalid/ownership外EnhancedはOriginalへfallbackし、`UseShellExecute`失敗はModal、selection、focus、source/shared stateを変えずrecoverable statusへ変換する。

### 7.7 Settings / keyboard / accessibility

App SettingsはUnseen dots、Confirm before delete、editable key bindings、thumbnail status borders、安全なAbout/Diagnosticsを持つ。Favorite枠は独立ON/OFF + `#RRGGBB`で既定黄色、AI高画質化済み枠は独立ON/OFF + `rainbow | #RRGGBB`で既定虹色。Browser `805d6c9` / WPF `60ed739`以後はFavorite/Enhancedのdirty preference単位だけをlock内latest diskへmergeし、もう一方の同時更新を古いdraftで戻さない。Browser `343a364`以後は空patchを拒否する。shared `.cache/settings.json`の未知nested keyとextension fieldを保持し、malformed/future/busy書込を非破壊でRetryへ戻す。invalid chordはaction単位fallback、active-context conflict時は曖昧mapを採用しない。

既定binding:

| Action | Key |
| --- | --- |
| Modal previous / next / close | Left / Right / Escape |
| Favorite +1 / -1 / exact Lv1〜5 | F / U / Ctrl+1〜5 |
| Recycle current | Delete |
| Select all / clear | Ctrl+A / Ctrl+Shift+A |
| Reopen tab | Ctrl+Shift+T |
| Reorder focused tab | Alt+Shift+Left/Right |
| Modal flip / Original-Enhanced | H / E |
| Gallery zoom | Ctrl+Plus / Minus / 0 |
| Modal zoom | Plus / Minus / 0 |

TextBox、ComboBox、DatePicker、Button、Landing、Settings/Delete overlayはgallery global shortcutを抑止する。Delete/Settingsのbare Escapeは設定外の固定rescue。Modal rootはfocusableでTab/Control+Tabをcycleし、close後はopener→active Grid/List→Landing primaryの順で戻す。

### 7.8 WPF right-click / Ctrl+C / Album

現行mainのXAML/code-behindにproduct image context menuはない。Clipboard操作はsafe diagnosticsとPrompt/Negative/PNG metadata textで、画像file/bitmap copyではない。headerのAlbum controlは有効でbasic libraryを開く。Browser/WPFは同じAlbum v1 store/operationを使い、create/rename/delete/pin/cover/recent/add/remove、current/outside/missing表示、current memberのAlbum順navigation、別操作のmembership remove/source Recycleを提供する。WPF v1のoutside memberは明示unavailableで、任意absolute pathを黙って開かない。したがってcontext menuとCtrl+C画像copyはSection 14のPENDINGだが、Album v1はimplementedである。

## 8. Favorite / Seen契約

### 8.1 Favorite

Favoriteはcanonical path→integer levelのmapで、許容値は1〜5、0は「entryなし」。single、multi、Modal、Right Previewの全surfaceが同じmodelを使う。

| Action | Browser | WPF | State transition |
| --- | --- | --- | --- |
| Increase | Fまたはbutton | Fまたはbutton | `min(5, current+1)` |
| Decrease | Uまたはbutton | Uまたはbutton | `max(0, current-1)`。0はmapから除去 |
| Exact level | Ctrl/Cmd+1〜5、UI | Ctrl+1〜5、UI | selected pathすべてをexact levelへ |
| Clear | Lv0/Clear | Lv0/Clear | selected pathすべてをmapから除去 |

client UIは即時反映できるが、共有write失敗時は「保存済み」と表示しない。Browserのlocal mirrorとshared diskはhydration時にmergeし、external writerの未知pathを不用意に落とさない。WPFはgeneration-aware writerを使い、終了時flushをboundedに待つ。shared writerは同じlock/tmp namingとmalformed-file保護を使う。

Favorite levelのdecrease/Clear/Lv0は評価値の変更であり、確認なしで実行する。**Favorite削除必須確認**は「Favorite levelが1以上のsource画像をRecycleする時」の保護を指す。現行mainでは通常のConfirm before delete設定がOFFでもsingle/bulkのFavorite source Deleteを必ずalert dialogへ通し、action boundaryでも`favoriteConfirmed`なしの直接実行を拒否する。Favorite mandatory dialogでは`Do not ask again`を表示/有効化しない。

### 8.2 Seen

Seenはcanonical path→trueのgrow-only markerである。次で追加する。

- gallery card/rowを明示clickまたはkeyboard selectionした時
- Modalを開いた時、Modalで前後移動した時
- active tab/previewから明示的に対象を表示した時

scan、search結果生成、viewportに入っただけ、thumbnail warmup/decode、Favorite変更だけではSeenにしない。Browserはlocal mirrorへ先に追加してshared `PUT /api/seen`でunionし、WPFも共有storeの既存entryを保持したmergeを行う。falseで削除するAPIは持たない。malformed共有fileは上書きせずrecoverable errorにする。

## 9. Delete / external open / drag契約

### 9.1 Source Recycle

source削除はhard deleteではなくWindows Recycle Binだけを使う。singleまたはbulk actionは、active catalog membership、canonical path、active root containment、supported regular file、existenceを再検証してから実行する。symlink/reparse escape、directory、catalog外、missing path、root外は拒否する。

- confirm settingがONならsingle/bulkともalert dialogを出す。設定がOFFでも対象にFavorite Lv1〜5が1件以上あればmandatory alert dialogを出す。Favorite mandatory時は`Do not ask again`を使わせず、execution boundaryでも明示confirmed flagがなければ拒否する。Cancelはsource、selection、catalog、tab、Favoriteを不変にする。
- success後だけcatalog、selection、Modal order、right preview、preview tabs/pins/closed-tab history、memory/disk image cacheをreconcileする。
- bulkはsuccess/failure countと各失敗理由を表示し、1件失敗で残りをhard deleteへfallbackしない。
- Modal current削除後はpre-delete full orderのnext、なければpreviousへ進み、0件ならModalを閉じる。
- sourceが外部消失した場合もFavorite、Seen、Enhancement historyは自動消去しない。

### 9.2 Managed Enhancement output delete

managed output削除はsource Recycleと別操作。jobがterminal succeededで、pathが`.cache/enhance/outputs/**`内、known job ownershipを満たす時だけ削除する。source path、任意path、unfinished outputは拒否する。削除後はjob recordを保持し、output missing/deleted stateとしてUIを更新する。

### 9.3 Open / Show in folder / drag

Open externalとExplorer revealはDeleteと同じsource guardを再実行し、shell command文字列を連結しない。Modal Open/Enterは現在表示中Original/Enhancedを容量表示と同じresolverで選び、Enhancedにはsource identity/signatureとlexical/real canonical managed ownershipを追加検証する。無効なEnhancedはOriginalへfallbackしてstatusを出し、launch failureでModal、selection、focus、source/shared stateを変更しない。Explorer revealはsource identityを対象にする。

Browser drag-outは`text/uri-list`、plain absolute path、`DownloadURL`を提供するが、OS native file copyと同等とは保証しない。WPF FileDropはfolderだけを受け付け、sourceをcopy/move/deleteしない。画像drag-outの正式契約は現行WPFにない。

## 10. Enhancement契約

### 10.1 絶対分離

通常browse、scan、search、sort、filter、scroll、selection、preview、Modal open/navigation、thumbnail/display decode、startup/restartはjob enqueueもworker startもしない。作成できる入口は明示Enhance action、設定済みEnhance shortcut、API POST、明示Retryだけ。

sourceはread-only。outputは`.cache/enhance/outputs/**`、job recordは`.cache/enhance/jobs.json`。adapterはComfyUIを含められるが、PhotoViewerをupscalerへ改名しない。WPF通常閲覧はBrowser不要で、WPFの明示AI x2だけがloopback Browser APIへ委譲する。WPF自身はjob engine/storeを所有しない。

### 10.2 Job state machine

```text
queued -> running -> succeeded
   |         |           |
   +-------> canceled    +-> output-deleted
   |         |
   +-------> failed --explicit retry--> new queued job
```

- retryは元recordを書き換えず、新jobを作る。source signature変更時は再作成を要求する。
- queued jobはprocess restart直後に自動resumeしない。runningのまま残ったrecordは次回queue処理時にinterrupted failureへ正規化する。
- cancelはadapterが安全に特定できるjobだけを対象にする。曖昧なglobal interruptはwarningを出す。
- output decode失敗またはmissingはsource表示へfallbackし、source/modal/navigationを失わない。
- Windowsでcompleted outputのatomic renameが`EBUSY`、`EPERM`、`EACCES`になった時は25/50/100/200/400/800/1600msで限定retryする。解放されなければdestinationへのfully-awaited copyを行う。copy完了時点でoutput publishは成功であり、temporary cleanupは別のbounded retryとして扱う。cleanupだけが引き続きlockされた場合はstale temporary residueを診断へ残すが、完成済みdestinationをjob failureへ戻さない。jobをsucceededにするのはrename/copy完了後だけで、copy自体の失敗とcleanup失敗を同じerrorへ偽装しない。その他のfilesystem errorは隠さずfailedへ返す。
- ncnn-vulkan adapterはnormalize後の寸法を、すでに取得したsource metadataまたはSharp `toFile()`の完了情報から確定する。書き込み直後のtemporaryをmetadata取得だけのために再openしてWindows lock時間を増やさない。
- queueはjob/errorがある時またはユーザーが明示openした時だけ表示する。
- Favorite/Enhanced thumbnail枠はshared `thumbnailStatusBorders` schemaを使う。Favorite既定黄色inner、Enhanced既定虹色outerを独立ON/OFF/単色設定でき、保存はdirty preference単位のlatest-disk mergeとする。Enhancement job lifecycleやsource/output ownershipとは別設定である。

## 11. Error / empty / large / restart matrix

| Scenario | Browser current contract | WPF current contract | Reimplementation MUST |
| --- | --- | --- | --- |
| folder set empty | Open disabled、Landing維持 | Open disabled、Landing維持 | scanを開始しない、draftを失わない |
| folder内supported image 0 | empty Viewer/Landing copy | empty catalog status | request failureと区別する |
| query/filter 0 result | empty result copy | empty result copy | catalogやFavorite/Seenを消さない |
| scan root一部失敗 | survivor/cacheを使いwarning | survivor catalogをpublishしskip記録 | root/file別に理由を残す |
| 全root失敗 | Landing error、Retry/Dismiss | Landing/status error | draftとrecentを保持、spinnerを終える |
| Browser search failure | 直前の成功結果とnon-modal notice | N/A | stale responseを破棄しRetry/Dismissを出す |
| Browser search session 410 | 手動Rescan | HTTP sessionなし | 直前結果を保持し、fresh tokenまでRetryと区別する |
| Browser enlarged-image session 410 | 1回通知→Viewer保持の同folder自動scan→fresh token。失敗時manual recovery | HTTP sessionなし | expired URL loop禁止、selection/Modal/shared state維持 |
| decode failure | placeholder/error、stale bitmap不採用 | placeholder/status、次画像へ進める | navigation/catalogを壊さない |
| sourceが外部消失 | UI参照をreconcile | UI参照をreconcile | shared historyを勝手にpurgeしない |
| malformed shared JSON | original保持、write拒否、local fallback/status | original保持、default/local fallback/status | malformed fileを空objectで上書きしない |
| Search History missing | empty history popup | empty history popup | current queryとViewer stateを変更しない |
| Search History malformed/future | empty + protected status、write拒否 | empty + protected status、write拒否 | original bytes/unknown fieldを保持する |
| cache/index corruption | cache missとして再生成 | persistent metadata indexをInvalid扱いしsource fallback | source/stateを削除しない、exceptionをUIへ漏らさない |
| concurrent write conflict | lock/tmp + merge/retry | lock/tmp + generation writer | timeout後に未保存を明示しunknown fieldを保持。Search Historyはlock内latest read + atomic replaceでlost entry 0 |
| very large library | silent truncate禁止、virtual/sparse paging | exact full catalog + virtualized realization | total、tail、order、selectionをexactに保つ |
| app restart | folder/view/shared state復元、Modalは自動openしない | ViewerState/shared/index復元、Modalは自動openしない | stale process/session IDを永続化しない |
| Enhancement server unavailable | explicit actionだけ失敗表示 | explicit AI actionだけ失敗表示 | ordinary browsingを失敗させない |
| launcher artifact stale | rebuildまたはfail、旧build禁止 | rebuildまたはfail、旧exe禁止 | UIの見た目だけでrevisionを推定しない |

## 12. Performance / scalability / responsiveness

### 12.1 共通pass condition

- indexed totalはfixture totalと一致し、silent truncate、duplicate、order driftを0にする。
- 1,201枚目以降と末尾画像をsearch、Grid/List、preview、Modalで到達できる。
- realized visual/control数はviewport近傍にboundedで、全catalog件数分のUI objectを作らない。
- background metadata、thumbnail、warmupはvisible/current actionより低priority。
- cancel/generation change後の古いsearch/decode/metadata completionはcurrent UIを書き換えない。
- first usable gallery、selection-to-preview、search、forward/back scroll、working set、GC、dispatcher/event-loop gapを同じhardware/build/fixtureで記録する。記録値を環境非依存の絶対合否へ読み替えない。

### 12.2 Browser

thumbnail object URL cacheはcompleted 2,400、displayは160、同一fetchはshared pending、最後のconsumer releaseでabortし、LRU evictionでURL revokeする。warmupはfocused > visible > nearby、40ms batch、3,500ms dedupe、pending speculative最大1,200。background warm最大1,200は製品画像件数上限ではない。Sharp concurrencyはCPU基準4〜12、`PV_THUMB_CONCURRENCY`で1〜16。5,000画像は推奨stress protocolで、現行source由来の固定時間閾値ではない。

Modal filmstripは100,000 logical itemsでもviewport + overscanだけをDOMへ出すunit gateを持つ。production E2Eは120-item fixtureで遠方index click、Arrow往復、Delete隣接移動が二重発火しないこと、current追従、chrome hide、close/reopenとlocalStorage保存を確認する。`9d8acb0`時点のhistorical E2Eは当時の3秒auto-hideを確認したが、現行manual visible / 900ms transient / hidden hover overlayを旧挙動へ読み替えない。2026-07-20のcurrent-tree isolated E2Eは通常port 3000を避けた`127.0.0.1:31337`で1/1 PASSし、manual表示固定、hidden cursor、900ms transient、下端overlay geometry、Search History input/row keyboard、遠方Filmstrip、Delete一段移動、console problem 0を同じ実Browser経路で確認した。reload hydrationは`ImageContext`のunmount/remount unit gateで確認する。この120-item E2Eを製品件数上限と解釈しない。

### 12.3 WPF

5,000件を通常integrated、20,000件をaggregate stress、large-catalog変更closeoutをexact 100,000 images / 100 foldersで検証する。exact gateはcatalog/filtered/Grid ItemsSourceすべて100,000、tail index 99,999、Grid/List/Created/Modal canonical path一致、silent truncate 0、bounded realizationを要求する。

共有Favorite/Seen 100,000-entry actor gateはModal next p95 50ms以下、Favorite action p95 65ms以下、dispatcher max gap 110ms以下、かつ各metricで`large <= max(control*2.5, control+10ms)`。logical Ctrl+A/Clearは各1,500ms以内、canonical selected exact 100,000、visual projection 2,048未満。

local mainの記録済み最終観測値は、cold catalog 3,809ms / metadata 26,659ms / full 30,850ms、warm catalog 3,396ms / metadata 213ms / full 3,928ms、warm hit 100,000 / miss 0、Grid/List realization 15/9、tail 99,999、anchor drift 0px。これは2026-07-19 closeout fixtureの証拠であり、別machineの性能保証ではない。

## 13. Browser local API contract

全routeはloopback trust boundary内だけで使う。`path`や`dir`を受け取るrouteは、documented legacy例外を除きcurrent `indexToken` snapshot membership、active root、canonical regular fileを検証する。error responseへsecret/envを含めない。

| Endpoint | Method / success | Required behavior |
| --- | --- | --- |
| `/api/runtime` | GET 200 | no-store safe provenance。absolute project rootを返さない |
| `/api/browse?multi=1` | POST 200 | native folder picker。cancelは空結果でstate非変更 |
| `/api/scan?dir=...&full=1` | GET SSE | progress/complete/error。completeでopaque indexToken |
| `/api/search` | GET 200 / 410 | token snapshotだけを検索。paged sparse order、410 expired |
| `/api/folders` | GET 200 | token対応、max 200。現行Sidebar callerのtoken欠落は既知gap |
| `/api/tags` | GET 200 | token対応、max 2,000。UI表示は上位8件 |
| `/api/image` | GET bytes | thumb/display/original。membership/type/containment guard、cache validator |
| `/api/thumbs/warm` | GET/POST | status / bounded explicit warmup。通常UIをblockしない |
| `/api/open` | GET/HEAD/POST 200 | active index/session membership、source type/existenceをguardし、Originalまたはsucceeded managed Enhancedを解決。GET/HEADは容量readだけ、POSTだけが外部appを起動。Enhancedはsource identity/signature、lexical+real final-path ownershipを検証し、不正/missing/staleはOriginal + recoverable fallback。shell failureは500、文字列連結禁止 |
| `/api/delete` | DELETE 200 | guarded Recycle Binのみ。hard-delete fallback禁止 |
| `/api/favorites` | GET/PUT | `{favorites}` wrapper、level validation、malformed保護、atomic shared merge |
| `/api/seen` | GET/PUT | `{seen}` wrapper、true markerだけ、union、malformed保護 |
| `/api/settings` | GET/PUT | default hydration、known field validation、unknown field保持、partial update |
| `/api/recent-folders` | GET/PUT | version 1 bounded last/recent folder sets、malformed保護 |
| `/api/search-history` | GET/PUT/DELETE | GET list。malformed/future/read failureはHTTP 200 + `ok:false`。PUT complete query commit/MRU、DELETE oneまたはclear。raw query過大等invalidは400、mutation時のprotected documentは409、mutation lock/I/O failureは503 |
| `/api/albums`、`/api/albums/:id`、`.../members`、`.../recent` | GET/POST/PATCH/DELETE | full snapshot PUTなし。create/rename/delete/pin/cover/add/remove/bulk/recentをoperation単位で実行し、optional expected revision、protected 409、Busy/I/O 503を返す |
| `/api/albums/:id/source` | POST 200 | active catalog tokenとAlbum membershipを検証してcurrent/outside/missingを分類し、current+guarded outsideだけのopaque source sessionを作る。raw path URLを返さない |
| `/api/albums/members/cleanup` | POST 200 | PhotoViewer source Recycle成功後だけcanonical pathを全Albumから除外する。Recycle失敗や外部消失では呼ばずtombstoneを保持 |
| `/api/legacy-state` | GET 200 | bounded empty legacy response。browser profileを直接読まない |
| `/api/enhance/presets` | GET | preset inventory |
| `/api/enhance/isolation` | GET | enqueue/worker metricsとqueue state |
| `/api/enhance/jobs` | GET/POST | list / explicit create only。POSTは202 |
| `/api/enhance/jobs/:id` | GET | one job / 404 |
| `.../:id/cancel` | POST | guarded cancel + interrupt warning |
| `.../:id/retry` | POST 202 | new job、changed sourceは409 |
| `.../:id/output` | DELETE | managed terminal outputだけ |
| `/api/enhance/output` | GET | known succeeded managed outputだけ |

## 14. 採用済み要件と実装確認待ち

2026-07-20にlocal `refs/heads/main`はWPF gallery zoom/geometry anchor実装 `e371b482af44e0428d9fe0d5217b236801f29cff`、shared-state latency `5ae1e00`、表示中asset open/容量表示 `a1d83c8`、WPF focus assertion `dbad550`、Modal button Enter isolation `452ac02`、Browser shared-writer FIFO fairness `0802f95`を含む後続main変更まで採用された。下表のimplementedはこのsourceと採用gateへ限定する。

### 14.1 implementedへ昇格したもの

| Requirement | Browser local main | WPF local main | Canonical status / evidence boundary |
| --- | --- | --- | --- |
| Favorite画像のsource削除必須確認 | single/bulk UI + `deleteImage` action boundaryで強制。通常確認OFFでも必須 | single/bulk dialog + execute boundaryで強制 | **implemented**。Favorite level ClearではなくFavorite source Recycle保護。mandatory時Do not ask again不可 |
| 拡大画像session自動回復 | image 410を1回通知しViewer保持scan、fresh token、失敗時manual recovery | HTTP session非該当 | **implemented (Browser)**。search 410のmanual Rescanは別contract |
| 20px〜最大1列 | 20〜600、step 20。600で1列、persisted clamp | 20〜600、step 20。600で1列、旧40維持・範囲外clamp | **implemented**。両surfaceでGridだけを変更しListを拡縮しない |
| Sidebar開閉アンカー | geometry changeを検出し同じanchor path/offsetを復元 | canonical full path+viewport offsetをSidebar/right panel/window resize/DPIで復元 | **implemented**。selection有無、同名別folder、List非破壊をfocused/stress gateで確認 |
| Enhancement EBUSY publish retry | transient rename retry→awaited copy fallback。copy完了後のtemporary cleanupは別にbounded retryし、cleanup-only lockで完成済みoutputをfailedへ戻さない。temporary metadata再openを回避 | 明示actionはBrowser APIへ委譲 | **implemented**。source非上書き、publish完了前にsucceededにしない。focused 4 files / 23 testsはgreenだがfresh real-GPU rerunはpending |
| Browser Modal virtualized filmstrip | current追従、direct click、bounded virtualization、T/toolbar開閉、保存、Arrow navigation。manual時専用下段、hidden時下端overlay、manual表示固定、hidden cursor + 900ms transient、hidden-state維持、root上端zoom | WPFも同じUI状態とFilmstrip geometryを採用 | **implemented**。Browser 100k logical/unit + current isolated production E2E 1/1、current full unit 63 files / 589 tests。WPF focused smoke全true |
| Browser/WPF shared Search History v1 | focus/click list、whole-query replacement、input/row keyboard、delete/clear、API、protected status、同一process FIFO | nonblocking popup、keyboard/list selection、delete/clear、protected/Busy status | **implemented**。raw/normalized 32,768 UTF-16境界、明示trim parity、version 1/max50/NFKC共通identity、unknown保持、barrier付きcross-runtime 20+20実overlap/lost 0、Browser同一target 30並列全200/lost 0、WinForms非参加 |
| Favorite / AI高画質化thumbnail枠 | Favoriteは既定黄色2px inner、Enhancedは既定虹色3px outer。独立ON/OFF、単色選択、Grid/List、dirty preference latest-disk merge、空patch拒否 | Browserと同じshared schema、Freeze済み虹brush、dirty preference latest-disk merge、同じinner/outer優先順 | **implemented** (`2863519`, `a091ec7`, `805d6c9`, `60ed739`, `343a364`)。旧Enhanced hex値互換、unknown/malformed/future/busy保護、同時表示、lost update防止、追加image I/Oなし |
| Modal manual/transient UI + Filmstrip parity | manual表示固定、非表示cursor、900ms transient、下端overlay、hidden-state維持 | 同じmanual/transient契約、専用row/overlay、button-focus shortcut、ViewerState保存 | **implemented** (`f6f63d3`, `a091ec7`, `c3d4ff5`)。WPF focused smokeで全項目true |
| 表示中Original/Enhancedの容量とexternal open | exact `0.00MB`、toggle即更新、GET/HEAD passive、POST-only launch、active index/type/existence + managed lexical/real ownership | exact `0.00MB`、toggle即更新、canonical source/signature/managed ownership guard、ShellExecute failure回復 | **implemented** (`a1d83c8`)。missing/stale/invalid signature/ownership外EnhancedはOriginal容量 + recoverable status。Browser focused 5 files / 71 tests、typecheck/lint/build green。WPF external-open/modal-enhancement/modal-interaction focused green、Release 0/0 |
| WPF Grid/List Enter → Modal | current filtered/sorted orderのprimaryを開く | Grid/List Enter、navigation後closeもcurrent primary itemへfocus、Search/Date/Settings/Delete/Modal input/Landing隔離 | **implemented** (`a1d83c8`, `dbad550`, `452ac02`)。`verify-wpf-gallery-enter-modal.ps1`と関連Modal verifier green。button focusのEnter/Space native activationを維持 |
| Album v1 / collection | operation API、library/picker、guarded source session、current/outside/missing、Album順Gallery/Modal/Filmstrip、remove/Recycle分離、Recycle成功後cleanup | shared storeとbasic library、create/rename/delete/pin/cover/add/remove、current-only Album順filter/Modal、outside/missing unavailable、catalog復帰、Recycle成功後cleanup | **implemented**。opaque id、revision/lock/atomic/conflict/unknown保持、collision-aware shortcut、Browser隔離E2E 1/1、WPF UI smoke、barrier付き16+16同時writer lost 0、Album 100k store test、WPF exact 100k catalog green |

今回追加分の採用gateはBrowser unit 61 files / 521 tests（3 files / 3 tests skip）、typecheck、production build、lint 0 errors、production Playwright 7/7、WPF Search History focused、Browser/WPF 20+20同時writer、Release build 0 warnings / 0 errors、WPF zoom promotion aggregate + reload soak 53/53、reload 24/24がgreen。WPF zoom/anchorはfocusedとexact 100,000 images / 100 foldersもcurrent mainで再実行した。後続shared-state latency descendantは`-SkipStress` aggregate 51/51とfocused latency 6/6がgreen。さらにdisplayed-asset successorの12 owned feature blobsがcheckpoint `8ff1e52`とcurrent mainでbyte-identicalであり、`-IncludeReloadSoak` aggregate 55/55、`AGGREGATE_EXIT=0`、333,899ms、reload soak 24 cycles / 40,322msがgreen。layout/state/stress実装・verifierは変えていないためexact 100,000/100 folders証拠を継承する。通常Browser/WPF launcherの最終採用は同日のtruth tableに示す。

後続deltaの証拠は上のmilestone件数と混同しない。`9d8acb0`のModal layoutはisolated production port 3001で1/1 PASS、`3654b88`のEnhancement publish/cleanupはfocused 4 files / 23 tests PASS。`a1d83c8`のBrowser displayed-asset focusedは5 files / 71 testsとtypecheck/lint/build、WPF external-open/gallery-enter/modal-enhancement/modal-interaction/status-border focusedとRelease 0/0がgreen。`0802f95`後のBrowser aggregateは63 files / 584 tests PASSだった。2026-07-20 completion audit後のcurrent treeは63 files / 589 tests PASS（3 files / 3 tests skip）、Next.js 16.2.10 production build、typecheck、scoped lint、Search History focused 33/33、isolated Playwright 1/1がgreen。WPF `-IncludeReloadSoak`は55/55、462,762ms、reload 24/24、cross-runtimeはready barrier後に592〜668msの実write overlapを観測した。fresh real-GPU jobは未実施なので、unit証拠を実machineのend-to-end成功へ読み替えない。

### 14.2 引き続き「実装確認待ち」

| Requirement | Browser local main | WPF local main | Canonical status / acceptance |
| --- | --- | --- | --- |
| image context menu | なし | なし | **実装確認待ち / pending**。selection rule、keyboard invocation、action availability、focus/Escape、Delete確認を定義 |
| Ctrl+C画像copy | metadata textのみ | diagnostics/metadata textのみ | **実装確認待ち / pending**。bitmap/fileのどちらか、selection、clipboard failure、text input isolationを定義 |

branch、別worktree、未採用commit、テスト単体の存在ではstatusを上げない。今回昇格していないrowは、今後もlocal main source、focused test、必要なruntime/launcherが揃うまでimplementedへ混ぜない。

## 15. 禁止事項

1. `origin/main`、PR head、別worktreeをlocal `refs/heads/main`の代わりに正本扱いしない。
2. Section 14の予定、prototype、unadopted commitを実装済みへ混ぜない。
3. passive browse/preview/modal navigationからEnhancementをenqueue/startしない。
4. source画像を上書きしない。Recycle失敗をhard deleteで補わない。
5. root/catalog guardを省略して任意pathをopen/delete/serveしない。
6. malformed shared stateをempty stateで上書きしない。unknown fieldを無断dropしない。
7. user state/cache、runtime、process、deploymentをverification都合で変更しない。
8. library件数をpaging、warmup上限、virtualization都合でsilent truncateしない。
9. stale async resultをcurrent selection/preview/catalogへ適用しない。
10. BrowserをLANへbindしない。WPF通常閲覧をBrowser/Node必須にしない。
11. WinFormsを削除/archive/通常gateへ戻さない。FROZEN解除をissue、branch、taskから推測しない。
12. manual/component testだけでlauncher/runtime採用を断言しない。

## 16. Source map

| Area | Browser evidence | WPF evidence |
| --- | --- | --- |
| shell/state | `src/app/page.tsx`, `src/store/ImageContext.tsx`, `src/lib/types.ts` | `local-native/PhotoViewer.Wpf/MainWindow.xaml`, `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs` |
| Landing/scan/session | `src/app/page.tsx`, `src/components/ScanProgressStatus.tsx`, `src/components/ScanErrorNotice.tsx`, `src/app/api/scan/route.ts`, `src/lib/indexer.ts`, `src/lib/scanRunCoordinator.ts` | `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs`, `local-native/PhotoViewer.Wpf/MetadataIndexStore.cs` |
| search/tags/folders/history | `src/components/SearchBar.tsx`, `src/lib/searchHistory.ts`, `src/app/api/search-history/route.ts`, `src/components/Sidebar.tsx`, `src/app/api/search/route.ts`, `src/app/api/tags/route.ts`, `src/app/api/folders/route.ts` | `local-native/PhotoViewer.Wpf/SearchHistoryStore.cs`, `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs`, `local-native/PhotoViewer.Wpf/MetadataIndexStore.cs` |
| Grid/List/virtualization | `src/components/ImageGrid.tsx`, `src/lib/imageListState.ts`, `src/lib/viewerUi.ts`, `src/lib/thumbnailSizing.ts` | `local-native/PhotoViewer.Wpf/VirtualizingWrapPanel.cs`, `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs` |
| preview/tabs | `src/components/RightPreviewPanel.tsx`, `src/components/BottomPreviewTabs.tsx` | `local-native/PhotoViewer.Wpf/MainWindow.xaml`, `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs` |
| Modal | `src/components/ImageModal.tsx`, `src/components/ModalFilmstrip.tsx`, `src/lib/modalNavigation.ts`, `src/lib/displayedAsset.ts` | `local-native/PhotoViewer.Wpf/MainWindow.xaml`, `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs` |
| Settings/keys | `src/components/SettingsModal.tsx`, `src/lib/keyBindings.ts`, `src/app/api/settings/route.ts` | `local-native/PhotoViewer.Wpf/KeyBindingSettings.cs`, `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs` |
| Album v1 | `src/store/AlbumContext.tsx`, `src/components/AlbumLibrary.tsx`, `src/components/AlbumPicker.tsx`, `src/lib/albums.ts`, `src/lib/albumSource.ts`, `src/app/api/albums/**` | `local-native/PhotoViewer.Wpf/AlbumStore.cs`, `local-native/PhotoViewer.Wpf/AlbumLibraryWindow.cs`, `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs` |
| Favorite/Seen/Recent | `src/store/ImageContext.tsx`, `src/app/api/favorites/route.ts`, `src/app/api/seen/route.ts`, `src/app/api/recent-folders/route.ts`, `src/lib/fileWriteLock.ts`, `src/lib/sharedProjectRoot.ts` | `local-native/PhotoViewer.Wpf/SharedStoreWriter.cs`, `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs`, `local-native/PhotoViewer.Wpf/App.xaml.cs` |
| Delete/open | `src/app/api/delete/deleteHandler.ts`, `src/app/api/delete/route.ts`, `src/app/api/open/route.ts`, `src/lib/activeImagePath.ts`, `src/lib/favoriteDeleteProtection.ts` | `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs` guarded shell/Recycle helpers |
| Enhancement | `src/lib/enhance/**`, `src/lib/enhance/outputPublish.ts`, `src/app/api/enhance/**`, `src/components/EnhanceQueuePanel.tsx` | `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs` Browser delegation/read-only history |
| launcher/provenance | `start_viewer.bat`, `scripts/prod_launcher*.js`, `scripts/verify-browser-runtime.ps1`, `src/app/api/runtime/route.ts` | `start_wpf.bat`, `scripts/check-wpf-launch-target.ps1`, `scripts/verify-wpf-launcher-freshness.ps1` |
| WinForms FROZEN | N/A | `local-native/PhotoViewer.Native/**`, `start_winforms.bat`, `scripts/start-local-native.ps1`。通常WPF evidenceへ混ぜない |

## 17. Test / acceptance対応表

| Contract | Browser gate | WPF gate | Current evidence boundary |
| --- | --- | --- | --- |
| static UI guard | `scripts/verify-ui-regression-guard.ps1` | aggregateから同guard | local main記録済みPASS |
| unit/component | `pnpm test:unit` | headless app smoke群 | Browser current 68 files / 614 tests green、3 files / 3 tests skip。過去milestone snapshotは履歴として保持 |
| type/lint/build | `pnpm typecheck`, `pnpm lint`, `pnpm build` | `dotnet build -c Release` | Browser typecheck/build green、lint 0 errors（unrelated Claude worktree 1 warning）、WPF 0 warning・0 error |
| Landing/recent/runtime | `e2e/home.spec.ts`, `verify-browser-runtime.ps1` | landing/recent verifier、launcher freshness | Browser isolated production Playwright 7/7。通常runtime evidenceはtruth tableへ記録 |
| Grid/List zoom | `ImageGrid.test.tsx`, `Sidebar.test.tsx`, `thumbnailSizing.test.ts`, `e2e/viewer-grid-zoom.spec.ts` | `verify-wpf-gallery-zoom-anchor.ps1`、catalog stress、gallery zoom/scroll/date/layout verifier | Browser/WPFとも20〜600/1列/geometry anchor採用。WPF current aggregate 53/53 + reload 24/24、exact100k/100folder green |
| Modal/Delete/open/capacity | `ImageModal.test.tsx`, `displayedAsset.test.ts`, `api/open/route.test.ts`, `RightPreviewPanel.test.tsx`, `favoriteDeleteProtection.test.ts`, delete integration/route tests | `verify-wpf-external-open.ps1`, `verify-wpf-gallery-enter-modal.ps1`, modal/delete verifier | Favorite source mandatory confirmation、displayed Original/Enhanced open、exact `0.00MB`、fallback、WPF Enter/focus/input isolationを採用。Browser 5 files / 71 tests + typecheck/lint/build、WPF focused all green + Release 0/0 |
| Modal filmstrip | `ModalFilmstrip.test.tsx`, `ImageModal.test.tsx`, `ImageContext.test.tsx`, settings tests、`e2e/viewer-modal-filmstrip.spec.ts` | `verify-wpf-modal-interaction.ps1` | 100k bounded DOM、current follow、click/Arrow/Delete/persistence、manual dedicated row、hidden hover overlay、900ms transient、cursor/hidden-state維持、top zoom、focused-button shortcuts。WPF focused全true |
| thumbnail status borders | status schema/API/Settings/Grid tests | `verify-wpf-thumbnail-status-borders.ps1` | Browser/WPF dirty preference latest-disk merge、empty patch拒否、lost update/unknown/malformed/busy保護を含めgreen。WPF Release 0/0 |
| Enhancement Windows publish | `outputPublish.test.ts`、ncnn-vulkan adapter tests | Browser API委譲のみ | copy publishとtemporary cleanupの成否を分離。focused 4 files / 23 tests green、fresh real-GPU rerun pending |
| Search History | route/SearchBar tests、実Browser keyboard、API live-lock timeout、同一target FIFO | `verify-wpf-search-history.ps1`, barrier付き`verify-cross-runtime-search-history.ps1` | async UI、input/row keyboard/a11y、raw/normalized上限、U+FEFF/U+0085 trim parity、max50、malformed/future保護、Busy writes 0、20+20実overlap/lost 0、Browser 30並列全200/lost 0 |
| Favorite/Seen/Recent concurrency | API route tests、2 cross-runtime scripts | same cross-runtime actors | 各20反復green、WinForms actorは含まない |
| key bindings/accessibility | component tests | `verify-wpf-key-bindings.ps1`等 | WPF editable/reload/reset/100k selection記録済み |
| Album v1 | Album store/source/API/component tests、`e2e/viewer-albums.spec.ts` | `verify-wpf-album-store.ps1`、aggregate、catalog stress | Browser unitを含む614 tests、隔離port 3131 E2E 1/1。Album focused 24/24、同時Browser/WPF 16+16 revision 32/lost 0、WPF UI order/availability/shortcut green |
| reload/state | component hydration/session recovery tests | state/reload aggregate | current WPF aggregate 56/56、reload 24/24、stale completion 0、CTS 73/73 green。exact 100k記録も維持 |
| large catalog | Browser 5,000推奨stress | 20,000 aggregate + exact 100,000/100 | WPF exact tail/order/realization/index記録済み |
| Enhancement isolation | API/isolation tests | passive-isolation checks | passive enqueue/start 0を要求 |
| Section 14採用/残件 | 14.1はlocal-main commitごとのfocused gate、14.2は専用test未完 | Favorite source確認、Search History、status枠、displayed asset、WPF一覧Enter、Album v1を採用。context menu/Ctrl+Cは未完 | surface単位のstatusを混ぜない |
| WinForms | 通常対象外 | 通常対象外 | FROZEN。重大破損/起動不能時だけ限定gateを新設 |

件数はsnapshotであって固定合格値ではない。suiteが増えたら全result 0 failureを要求し、古い件数へ合わせるためtestを削らない。E2Eはunit/componentより狭い。static testはOS Recycle Bin、shell、clipboard、focus、DPI、launcherの実動作を代替しない。

## 18. 再実装順序とchange control

1. canonical path、supported format、root/index guard、shared schemasを先に固定する。
2. Landing→scan→immutable catalog→search/filter/sortを作り、empty/error/cancel/generationを先に通す。
3. virtualized Grid/List、canonical selection、preview、tabs、Modalを同じfull orderへ接続する。
4. Favorite/Seen/Recent/Search Historyをshared writerへ接続し、malformed/concurrent/restartを通す。
5. guarded external openとRecycleを実装し、source非破壊を証明する。
6. Settings/keybinding/focus/accessibilityとlauncher provenanceを通す。
7. 最後に明示Enhancementを接続し、passive isolationを再検証する。
8. Section 14は各要件を別adoptionとして、local main commit、focused test、必要なlive runtime/launcher evidenceが揃ったものだけtruth tableへ昇格する。

仕様変更時はこの文書と `docs/current-implementation-truth.md` を同じcommitで更新する。既存の `docs/browser-feature-contract.md` と `docs/wpf-product-spec.md` は詳細根拠として残すが、矛盾時は本書の明示した現行差分とtruth tableを優先する。
