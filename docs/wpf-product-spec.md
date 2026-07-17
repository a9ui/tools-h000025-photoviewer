# WPF版 PhotoViewer 製品仕様・実装契約

文書状態: **Normative for WPF / implementation-ready**

最終監査日: 2026-07-18 JST

正本関係:

1. 製品意味、安全境界、Browser共通workflowは [browser-feature-contract.md](./browser-feature-contract.md)。
2. この文書は、その契約をWindows WPFへ適用した現在仕様とnative固有契約を定める。
3. 実装順と残差は [browser-to-wpf-parity-plan.md](./browser-to-wpf-parity-plan.md)。
4. 文書とlive codeが衝突した場合は、安全契約、focused verifier、live code、文書の順に確認し、同じ変更で文書を直す。

`local-native/ui-mockup/**`は初期shellの歴史資料であり非正規。そこに残るQuick Search、Today/7d/30d/year、threshold favorite chip、旧sidebar構成を現行仕様として復活させない。視覚QAはreal WPFの`--shot`出力と同一stateの現行Browserを使う。

## 1. 製品目的と非目標

WPF版PhotoViewerは、Windows上の大量のローカル生成画像を、serverやWebViewなしで高速に検索、比較、評価、整理するnative desktop viewerである。Browser版の見た目を機械的に複製するのではなく、ユーザーが既に覚えた意味と操作結果を維持しながら、native virtualization、Recycle Bin、Explorer連携、低遅延inputを利用する。

MUST:

- Favoriteはpathごとの整数0〜5。
- Favorite Lv1〜5 filterは独立exact-match。未選択はAll。
- Seenは明示閲覧時だけadditiveに増える。Unseen dotsは表示設定だけで既定OFF。
- gallery zoomはsidebar、header、font、List rowを変えない。
- zoom前後で基準画像のviewport位置を維持する。
- source DeleteはWindows Recycle Binのみ。hard delete fallbackを持たない。
- scan、preview、modal、tab hoverはEnhancement jobを作らずworkerを起動しない。
- 1,200件等のsilent product capを置かない。
- 既存state/cache/shared JSONを削除して移行しない。

NON-GOAL:

- cloud account/sync、remote公開、deployment
- source画像の編集・上書き
- permanent delete
- editable user tag database
- passive browsingからの自動Enhancement
- Browser HTTP serverの内蔵

## 2. Runtime、process、所有データ

- .NET 8 Windows WPF、single desktop process。
- main project: `local-native/PhotoViewer.Wpf/PhotoViewer.Wpf.csproj`。
- BrowserのNode/Next serverを起動しない。
- source folderをreadし、explicit Delete時だけShell Recycle Binを呼ぶ。
- screenshot/smoke用CLIは専用temp fixtureとoverride pathを受け、実ユーザーstate/cacheを使わない。

共有所有:

| Data | Path | Meaning |
| --- | --- | --- |
| Favorite | `.cache/favorites.json` | `absolutePath -> 1..5`。0はentryなし |
| Seen | `.cache/seen.json` | additive `absolutePath -> true` |
| Recent folders | `.cache/recent-folders.json` | Browser/WPF間のfolder-set履歴 |
| Enhancement jobs | `.cache/enhance/jobs.json` | WPFはread-onlyで成功outputを参照 |

WPF local stateはviewer window、filter、selection、tab等のnative stateを保存する。共有JSONとlocal stateを同じtransactionに混ぜない。

### 2.1 Visual languageとlayout

既存WPF design tokensを正本として使い、別AIは独自palette/radius/type scaleを発明しない。

| Token | Value |
| --- | --- |
| Window background | `#08080C` |
| Secondary surface | `#101018` |
| Tertiary surface | `#181824` |
| Elevated surface | `#1C1C2E` |
| Header | `#0B0B12` |
| Primary text | `#EEF0F6` |
| Secondary text | `#7A7F9A` |
| Tertiary text | `#50546E` |
| Accent | `#6366F1` |
| Accent light | `#818CF8` |
| Favorite | `#F43F5E` |
| Success | `#10B981` |
| Danger | `#EF4444` |
| Body font | Segoe UI |
| Mono values | Cascadia Mono → Consolas fallback |

Window/layout:

- default 1280×820、minimum 900×560、center start、custom 52px caption/header。
- bottom preview bar 40px。
- bodyはsidebar / gallery / keyboard-resizable splitter / right previewの列構成。
- default right preview 340px、240〜900px。
- icon button 36×36、caption button 44×34、common corner radius 6〜12、pill radius 999。
- focus/hover/selectedは既存Accent/GlassBorder/HoverFill tokenを使う。
- logoはindigo→purple gradient。Favoriteはrose系。dangerはred。新しいaction色を増やさない。
- landingは中央760pxのfolder-set panel、52px logo、local metadata説明、左右の低opacity radial glow。
- card/previewは画像を主役にし、metadataとactionはsecondary/elevated surfaceへ収める。

UIを変更した時は同じviewport/stateの既存screenshotと新screenshotを並べ、padding、crop、text truncation、border、focus、overlapを確認する。

```text
+--------------------------------------------------------------------------------+
| 52px Header: sidebar | logo | refresh | Search | count | settings/right/window |
+-------------+--------------------------------------+---+-----------------------+
| Sidebar     | Grid/List full-catalog viewport      | S | Right preview         |
| filters     | bounded visual realization           | p | single/bulk actions   |
| folders     | selection + zoom anchor              | l | metadata              |
+-------------+--------------------------------------+---+-----------------------+
| 40px Preview tabs: Reopen | Close all | ordered active/pinned tabs             |
+--------------------------------------------------------------------------------+
```

Landingとmodalはこのshellに重なるtop-level surface。Delete confirmationとApp Settingsはmodalより優先されるdialog surface。

## 3. 起動、Landing、folder set

### WPF-LND-001 Top-level phase

1. Landing: folder set編集、last/recent、paste、Add、Open。
2. Loading: recursive enumeration、metadata、catalog構築、initial realization。
3. Viewer: sidebar、gallery、right preview、bottom tabs、modal。

### WPF-LND-002 Folder set

- 複数rootを保持できる。
- Add folderは現在setへappendする。
- RemoveはLanding draftだけから除く。source folderを削除しない。
- Pasteは1行1absolute path。blankを除きcase-insensitive dedupeする。
- Openは現在setをscanする。
- Change folderは現在setを保持してLanding編集へ戻る。即replaceしない。
- Last folder setと最大8件のrecent setを表示する。
- recent shared fileがmalformed/future versionなら上書きせずrecoverable statusを出す。

### WPF-LND-003 Scan

- 各rootをrecursive enumerationする。
- symbolic link/reparse pointを意図せず追跡しない。
- permission/individual file failureは可能な項目を継続し、summary warningを出す。
- supported extension以外をcatalogへ入れない。
- scan cancellationは前loadのdecode/filterをcancelし、stale resultをUIへapplyしない。
- empty folderとscan failureは別copyにする。

## 4. 対応formatとmetadata

対応extension:

- PNG
- JPG/JPEG
- WebP
- AVIF
- BMP
- GIF
- TIF/TIFF

Decoderが個別画像を読めない場合、その画像だけplaceholder/recoverable warningにし、catalog全体を落とさない。format verifierは実fixtureを生成し、decode可否、fallback、他画像の継続を確認する。

PNGは最大4MiBのparameters text chunkを読み、次を抽出する。

- Prompt
- Negative prompt
- generation settings text
- sampler等の表示用summary

検索対象はfilenameとindexed PNG prompt。absolute path、negative prompt、settings全文は標準検索対象にしない。

## 5. Catalog、virtualization、性能

### WPF-CAT-001 全件model

- `_allTiles`はscanで見つけた全supported imageの軽量catalog。
- `_tiles`はcurrent query/filter/sort後の全order。
- `_gridTiles`はGridで実際にmaterializeするbounded window。
- search、selection、modal order、Delete neighborはvisual container数ではなく全件query orderを使う。

### WPF-CAT-002 Realization

- Grid initial realization: 96件。
- realization batch: 96件。
- Grid最大realized window: 384件。
- Listはvirtualizing/recycling panelを使う。
- containerを外してもcatalog/selection/Favorite/Seenを失わない。
- 5,000件fixtureで先頭、中間、末尾を往復して欠落、重複、order driftを起こさない。

### WPF-CAT-003 Decode concurrency

- metadata worker最大4。
- thumbnail worker最大12。
- current selection/previewをbackground thumbnailより優先する。
- decode resultはpath/generationを照合し、古いcompletionが新selectionを上書きしない。
- corrupt画像のfailureは他decode queueを止めない。

### WPF-CAT-004 Search responsiveness

- TextChangedから150ms debounce。
- immutable catalog snapshot上でfilter/sortをbackground計算する。
- generationとCancellationTokenでstale queryを破棄する。
- 最終queryだけがUI、selection、preview、stateへapplyされる。
- 5,000件をrapid queryしてもinput handlerをblockせず、dispatcher heartbeatが継続する。

## 6. Viewer shell

### WPF-SHL-001 Header

- app logo/landing return
- current folder-set summary
- indexed/shown count
- Search input
- sidebar visibility
- right preview visibility
- window controls

### WPF-SHL-002 Sidebar

Sidebarはgallery card zoomから独立した固定layout columnで、次を上から持つ。

1. Search補助状態
2. Favorites / Unrated / Lv1〜5
3. manual From / To
4. Folders section
5. Sort
6. Grid/List
7. Standard/Compact/Poster
8. Original/1:1/2:3
9. Gallery zoom
10. App Settings

存在してはならないUI:

- Quick Search
- Today
- 7d
- 30d
- This year

Foldersは既定展開で、header buttonから折り畳む。collapse中のchildは非表示かつTab対象外。展開/折り畳み状態はstate version 2へ保存し、version 1またはfield欠落時は展開へmigrationする。

## 7. Search、filter、sort

### WPF-SRC-001 Query grammar

- comma区切りtoken。
- trimしempty tokenを除く。
- token間はAND。
- case-insensitive substring。
- 各tokenは`filename + indexed prompt`へ一致する。
- `cat, night`はcatとnightの両方。
- `night cat`は空白込みの1token。

no-resultは正常empty state。permission/decode/query failureはerror state。

### WPF-FAV-001 Favorite filter

- Favorites OFF: level checkboxesは結果へ影響しない。
- Favorites ON + level未選択: All、level 1〜5すべて。
- Favorites ON + level選択: 選択exact levelのOR。
- Lv1〜5は独立multi-toggle。
- Unratedはlevel 0 exactでFavoritesと相互排他。
- threshold `Lv N+`は禁止。

### WPF-DATE-001 Date

- manual From/Toだけを表示する。
- Created/Birth timeを使う。Modified timeではない。
- Fromはlocal day start inclusive、Toはlocal day end inclusive。
- legacy Today/7d/30d/year tokenは起動互換のreaderにだけ残してよい。UIへ復活させない。

### WPF-FLD-001 Folder buckets

- current folder setからstable bucket key、label、countを作る。
- bucket単体show/hide、Show All、Hide All、Invertを持つ。
- native extended selectionでCtrl toggleとShift rangeを扱い、Show selected / Hide selectedを持つ。
- hidden setをstateへ保存する。
- selected bucket setとprimary bucketをstateへ保存し、reloadとsort後も有効なbucketだけを復元する。
- bucket filter変更でFavorite/Seenそのものを変えない。

### WPF-SORT-001 Sort

- Modified newest/oldest
- Created newest/oldest
- Name
- Random + seed/reshuffle

sort変更はgallery、selection reveal、modal、Delete neighborのorderを同時に変える。Randomは同じseedならreload後も同じorder。

## 8. Gallery、selection、zoom

### WPF-GAL-001 View/style/aspect

- Grid / Listを切替える。
- style: Standard / Compact / Poster。
- aspect: Original contain / 1:1 cover / 2:3 cover。
- List row/thumbnail寸法はGrid card zoomから独立する。
- view切替でselected set、primary、preview contextを失わない。

### WPF-GAL-002 Selection

- click: single selection + primary + Seen。
- Ctrl-click: toggle。
- Shift-click: anchor range。
- Ctrl+Shift-click: additive range。
- Grid/Listは同じselected path setを共有する。
- background clearは明示空白clickだけ。
- virtualization window更新中はselection eventsをsuppressし、selected setを消さない。
- 複数選択時はright panelをbulk surfaceへ切り替える。

### WPF-GAL-003 Favorite操作

- F: selected image(s)を+1、最大5。
- U: selected image(s)を-1、最小0。
- single actionはcurrent primaryへ適用。
- bulk surfaceはLv0〜5 exact set、Increase all、Decrease allを持つ。
- multi-selection summaryは件数とmixed/uniform levelを表示する。
- bulk writeは1回のshared merge transaction。途中の画像ごとにfileを書かない。
- disk latestの無関係path、unknown entryを保持する。
- malformed/lock timeout/write failureはmemory/diskをrollbackし、selectionを保持する。

### WPF-GAL-004 Seen/Unseen

- explicit selection、right preview表示、modal open/navigationでSeenをadditiveに付ける。
- scroll/realize/thumbnail decodeだけでは付けない。
- Unseen dots既定OFF。ONは未Seen itemへdotを表示するだけ。
- toggleで`.cache/seen.json`を消去・false化しない。
- shared writeはlatest diskとのunion、process lock、temp+replace。malformed fileは上書きしない。

### WPF-GAL-005 Gallery zoom

- range 40〜600、default/reset 200。
- Ctrl+wheel、Ctrl+`+`、Ctrl+`-`、Ctrl+`0`。
- zoom前にviewport基準画像とoffsetをcaptureする。
- reflow後に同じpathをrevealしoffsetを補正、scroll boundsへclampする。
- 100→300→80→200のfixtureでanchor driftを1 card未満、現行smokeでは0に保つ。
- sidebar/header/font/right panel/List row sizeは変えない。

## 9. Right preview

- 既定幅340、範囲240〜900。
- splitter mouse dragとkeyboard incrementに対応する。
- widthとopen/closed stateを保存する。
- single selection: image、filename/path、Favorite level/actions、Open、Open tab、metadata、source Delete。
- multi selection: count、level summary、bulk Favorite、bulk Recycle。
- selectionなし: empty guidance。
- preview decodeはimmediate placeholder + background decode。generation/path guardでstale completionを捨てる。
- PNG metadataもlatest selectionへだけapplyする。
- corrupt/open failureはright panel内のrecoverable statusにし、他画像を選べる。

## 10. Preview tabs

### WPF-TAB-001 Operations

- selected imageをOpen tab。
- tab activateでselection/right previewを同期する。
- pin/unpin marker。
- close、Close all、Reopen last closed。
- Ctrl+Shift+Tでreopen。
- closed stack最大30。
- hoverでthumbnail、filename、path。
- pointer dragで任意位置へreorderする。drop targetは既存accent glassで示す。
- focused tabはAlt+Shift+Left/Rightで1位置ずつreorderする。
- tab本体のmiddle-clickでcloseする。
- reorder後もactive/pinを維持し、同じtabへfocusを戻す。無効moveはorderを変えずinline statusを出す。

### WPF-TAB-002 Persistence

- ordered open tab paths、active path、pin path setをstateへ保存する。
- 最大30件、case-insensitive dedupe、supported absolute pathだけ。
- scan後のcurrent catalogに存在するpathだけを同じorderで復元する。
- activeが欠落なら先頭へfallbackする。
- reload restoreだけでmodalを開かない。
- closeしてもpin markerは残し、同じpathの再open時に復元する。

### WPF-TAB-003 Hover performance

- popup text/chromeは即時表示する。
- image decodeはbackground、cancel可能。
- rapid hover時は前decodeをcancelし、generation/pathが最新のresultだけapplyする。
- popup close後のcompletionはimageを再表示しない。
- corrupt/large inputはdispatcherをblockせず、inline recoverable statusを出す。

## 11. Modal

- current filtered/sorted full orderを使う。
- Left/Right、buttonでprevious/next。
- 先頭previousは末尾、末尾nextは先頭へwrap。
- navigationでSeenを付けるがEnhancement jobを作らない。
- zoom 0.25〜10、wheel/keyboard、reset。
- pan、horizontal flip。
- Original/Enhanced表示切替。成功済みmanaged outputだけを候補にする。
- Prompt / Negative / Settings metadata tabs、copy。
- Escapeで閉じ、元selectionへfocus/contextを戻す。
- Delete成功後はpre-delete orderのnext、なければpreviousへ継続。最後の1枚なら閉じる。
- zoom 1.0でdrag閾値未満のimage single-clickはtopbar/footer/left-right edge controlsをまとめてhide/showする。control clickはchrome toggleへ伝播しない。
- image double-clickはmetadata sidebar toggleを優先し、遅延single-click chrome toggleをcancelする。
- left/right 72px edge buttonはprevious/nextのAutomationName/HelpTextを持つ。
- zoom 1.0で水平移動が垂直移動を上回り、画像幅16%を72〜180pxへclampした閾値を越えたpointer gestureはprevious/next swipe。zoom > 1ではpanだけを行う。
- previous/next、zoom/reset、chrome hide/showは650msのtransient feedbackを既存accent/glass tokenで示す。

CURRENT OPEN:

- metadata prompt tag→search

## 12. Source Delete / Recycle

### WPF-DEL-001 Guard

Delete直前に毎回すべて再検証する。

1. pathをcanonicalizeできる。
2. current active root配下。
3. current catalog/indexに存在。
4. supported image extension。
5. actual fileが存在。
6. project/app rootや別rootへescapeしない。

### WPF-DEL-002 Execution

- Windows Shell Recycle Binだけを使う。
- hard delete API、`File.Delete` fallbackを持たない。
- confirm既定ON。
- confirmationはCancel、Recycle、Do not ask againを持つ。
- Recycle成功を確認してからcatalog/stateを更新する。
- failure/cancelではsource、catalog、Favorite、Seen、selection、tabsを変えない。

### WPF-DEL-003 Neighbor

- action前のcurrent filtered/sorted orderとprimary indexをsnapshotする。
- success後は同じorderのnext、なければprevious。
- filter外の画像へ勝手に飛ばない。
- last itemならpreview/modal/selectionをemptyへ整合する。

### WPF-DEL-004 Bulk

- multi-selectionのsnapshotを対象にする。
- count付きexplicit confirmation。
- 各pathを同じsingle guard/Recycle serviceで独立実行する。
- successful pathだけをcatalog/Favorite/Seen/tab/selectionから除去する。
- failed pathはsourceとUIに残す。
- resultはsucceeded/failed countと復旧可能な理由を表示する。
- all-or-nothingを偽装しない。hard-delete fallbackは0回。

実ファイルtestは必ず専用temp copyだけを使う。

## 13. Enhancement isolation

- WPFは`.cache/enhance/jobs.json`をreadし、succeeded jobのmanaged outputをsourceへ関連付ける。
- malformed job、missing output、unsupported outputでもsource browsingを継続する。
- scan、selection、preview、tab hover、modal navigationはjobs fileを変更しない。
- worker processを自動startしない。
- enqueue/cancel/retry/output deleteはP3製品判断まで実装しない。

## 14. Settings、state、persistence

### WPF-STATE-001 ViewerState v1

保存field:

- LastFolder / LastFolderSet
- SearchQuery / SelectedPath
- CardWidth
- RightPanelOpen / RightPanelWidth
- DisplayStyle / AspectMode
- SortBy / RandomSortSeed
- manual DateFrom / DateTo
- ShowFavoritesOnly / ShowUnfavoriteOnly / FavoriteFilterLevels
- ShowUnseenDots
- ConfirmBeforeDelete
- HiddenFolderBuckets
- PinnedPreviewPaths
- PreviewTabPaths / ActivePreviewTabPath
- unknown extension fields

旧scalar `FavoriteFilterLevel`はreaderだけ残し、同じexact single levelへadditive migrationする。Quick Search/date presetを復活させるmigrationはしない。

### WPF-STATE-002 Write safety

- UI changeをdebounceする。search/stateは300ms単位。
- targetごとのprocess lock、2秒timeout、25ms retry、30秒stale recovery。
- lock内でlatest diskを再読し、unknown fieldとexternal updateを保持する。
- temp fileへwriteしてatomic replace/renameする。
- malformed/future incompatible fileを空stateで上書きしない。
- failure時は既存fileを保持し、recoverable statusを表示する。

### WPF-STATE-003 Defaults

| Setting | Default |
| --- | --- |
| Grid card | 200 |
| Right panel width | 340 |
| Display style | Standard |
| Aspect | Original |
| Sort | Modified newest |
| Favorite levels | none = All |
| Unseen dots | OFF |
| Folders | expanded |
| Confirm before delete | ON |
| Modal zoom | 1.0 |

## 15. Keyboard、focus、accessibility

Global shortcuts:

| Action | Key |
| --- | --- |
| Favorite +1 | F |
| Favorite -1 | U |
| Recycle selected | Delete |
| Reopen tab | Ctrl/Win + Shift + T |
| Gallery zoom | Ctrl + wheel / + / - / 0 |
| Modal previous/next | Left / Right |
| Modal flip | H |
| Modal zoom/reset | + / - / 0 |
| Close top surface | Escape |

Guard:

- TextBox、ComboBox、DatePicker、Buttonへfocus中はgallery global shortcutを通さない。
- Delete/App Settings dialog中はdialog keyだけを優先する。
- modal transform keyはmodal表示中だけ。
- Ctrl/Shift selectionはnative selection semanticsを壊さない。

Accessibility:

- interactive controlはButton/Toggle/CheckBox等のnative peerを使う。
- icon-only controlはAutomationName/ToolTipを持つ。
- dialogはinitial focus、Escape、close後focus returnを持つ。
- bulk summary/errorはscreen readerが理解できる名前/statusを持つ。
- disabled actionは単に消すのではなく、必要なら理由をstatusへ示す。
- keyboardだけでLanding→Viewer→filter→selection→preview→tab→modal→settings→Delete confirmationを操作できる。

## 16. Error、loading、empty state

- recoverable errorはblocking MessageBoxを乱用せず、viewer/landing内statusへ表示する。
- statusはaction、原因、継続可否、retry可能ならretryを示す。
- persistence lock busyはbounded retry後に明示する。
- malformed Favorite/Seen/state/recentは元fileを保持し、書込みを拒否する。
- decode failureは該当画像だけ。
- scan partial failureは成功件数とskipを示す。
- normal zero-result、no supported images、scan failure、filter emptyを別copyにする。
- error後も他画像のselection/preview、folder change、settings closeが可能。

## 17. Verification contract

変更は最低限、影響workflowの専用verifierと次の共通gateを通す。

| Area | Command |
| --- | --- |
| Retired UI regression guard | `powershell -File scripts/verify-ui-regression-guard.ps1` |
| P0 integrated / 5,000 | `powershell -File scripts/verify-wpf-p0.ps1` |
| P1 search/date/folder | `powershell -File scripts/verify-wpf-p1a.ps1` |
| P1 error/a11y/state | `powershell -File scripts/verify-wpf-p1b.ps1` |
| Formats | `powershell -File scripts/verify-wpf-formats.ps1` |
| Right panel | `powershell -File scripts/verify-wpf-right-panel.ps1` |
| Bulk Favorite | `powershell -File scripts/verify-wpf-bulk-favorite.ps1` |
| Search responsiveness | `powershell -File scripts/verify-wpf-search-stall.ps1` |
| Preview tabs/reload | `powershell -File scripts/verify-wpf-preview-tabs.ps1` |
| Preview hover | `powershell -File scripts/verify-wpf-preview-tab-hover.ps1` |
| Preview tab reorder | `powershell -File scripts/verify-wpf-preview-tab-reorder.ps1` |
| Modal wrap | `powershell -File scripts/verify-wpf-modal-wrap.ps1` |
| Modal pointer/chrome | `powershell -File scripts/verify-wpf-modal-interaction.ps1` |
| Bulk Recycle | `powershell -File scripts/verify-wpf-bulk-recycle.ps1` |
| Folder bucket selection/persistence | `powershell -File scripts/verify-wpf-folder-buckets.ps1` |

共通pass条件:

- Release build 0 error。
- disposable fixture以外をDeleteしない。
- source count、catalog count、filtered order、selection、previewが期待通り。
- existing state/cacheを削除しない。
- malformed/lock/decode/Recycle failureで元データ保持。
- passive workflow前後でenhancement jobs hash不変。
- 5,000件でsilent truncateなし、realized UI bounded。
- screenshotは同一viewport/stateのBrowser referenceと並べて差分確認し、screenshotだけをQA完了証拠にしない。

## 18. 現在の完成境界

Browser基準のWPF P0/P1は実装・専用smoke済み。日常利用の主要workflowで追加済みのP2は、Bulk Favorite、Bulk Recycle、right panel resize、preview tab reload/hover/reorder/middle-close、modal end wrap/chrome/edge/swipe/feedback、Folder bucket range selection/selected actions/collapse persistence。

残るP2:

1. Prompt tag→search、suggestion/chip parity。
2. Native FileDrop drag-out。

P3として明示的に遅延:

- WPFからのEnhancement enqueue/worker/cancel/retry/output delete。
- 高度gesture、animation、visual polish。

「完成」を無期限に伸ばさないため、P0/P1安全契約と日常workflowを製品完成線、P2を操作効率、P3を製品判断後の拡張として分ける。残差を実装するたびにこの文書、parity ledger、focused verifierを同じcommitで更新する。
