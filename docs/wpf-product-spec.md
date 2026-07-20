# WPF版 PhotoViewer 製品仕様・実装契約

文書状態: **Normative for WPF / implementation-ready**

最終監査日: 2026-07-20 JST

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
- 通常起動入口はproject rootの`start_wpf.bat`。repo root、project/target path、git revision、WPF source fingerprint、Release exe SHA256をatomic provenanceで照合し、全て一致する時だけdirect executableを起動する。missing/unproven/invalid/wrong-worktree/wrong-revision/source drift/target改変はRelease buildへ戻り、buildまたはprovenance記録に失敗した時は旧exeを起動しない。appがexit 0ならconsoleをpauseせず終了し、非0またはproject missingだけ診断を残す。`PHOTOVIEWER_WPF_REBUILD=1`は明示rebuild、`PHOTOVIEWER_WPF_DOTNET_RUN=1`は開発用だけに使う。
- launcherはBrowser serverやportを開始・停止せず、既存WPF processの探索・終了もしない。process ownershipをport番号や同名exeだけから推測しない。freshness分岐は`scripts/verify-wpf-launcher-freshness.ps1`で固定する。
- BrowserのNode/Next serverを起動しない。
- source folderをreadし、explicit Delete時だけShell Recycle Binを呼ぶ。
- screenshot/smoke用CLIは専用temp fixtureとoverride pathを受け、実ユーザーstate/cacheを使わない。

共有所有:

| Data | Path | Meaning |
| --- | --- | --- |
| Favorite | `.cache/favorites.json` | `absolutePath -> 1..5`。0はentryなし |
| Seen | `.cache/seen.json` | additive `absolutePath -> true` |
| Recent folders | `.cache/recent-folders.json` | Browser/WPF間のfolder-set履歴 |
| Search History | `.cache/search-history.json` | version 1、complete queryのMRU最大50件。Browser/WPF read-write、ViewerStateとは別owner |
| Shared settings | `.cache/settings.json` | Browser設定文書。WPFは`thumbnailStatusBorders` subtreeだけをread-writeし、Favorite/Enhanced preference単位でlatest-disk merge |
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
- custom maximizeは現在windowが属するmonitorのtaskbarを除いたwork areaを使う。primary monitorへ飛ばさず、mixed-DPIではdevice pixelをWPF DIPへ変換し、monitor API失敗時だけ`SystemParameters.WorkArea`へ安全にfallbackする。restoreは同じwork areaに収まるboundsならmaximize前の値をexactに戻し、monitor切断、解像度低下、DPI変更で旧boundsがはみ出す場合は現在work area内へsize/positionを正規化する。cross-launchのwindow boundsは保存せず、起動時はcenter startとする。
- bottom preview bar 40px。
- bodyはsidebar / gallery / keyboard-resizable splitter / right previewの列構成。
- default right preview 340px、240〜900px。
- icon button 36×36、caption button 44×34、common corner radius 6〜12、pill radius 999。
- focus/hover/selectedは既存Accent/GlassBorder/HoverFill tokenを使う。
- logoはindigo→purple gradient。Favoriteはrose系。dangerはred。新しいaction色を増やさない。
- landingは中央760pxのfolder-set panel、52px logo、local metadata説明、左右の低opacity radial glow、右上のApp Settings入口。
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

1. Landing: folder set編集、last/recent、paste、Add、Open、App Settings。
2. Loading: recursive enumeration、metadata、catalog構築、initial realization。
3. Viewer: sidebar、gallery、right preview、bottom tabs、modal。

### WPF-LND-002 Folder set

- 複数rootを保持できる。
- Add folderは現在setへappendする。
- LandingとViewerはExplorerからのfolder FileDropを受ける。absolute pathをcanonicalizeし、existing directoryだけをcase-insensitiveにdedupeしてappendする。Landingはdraft folder setだけを更新し、Viewerは既存setへ追加してscanする。source file/folderのcopy、move、deleteは行わない。
- RemoveはLanding draftだけから除く。source folderを削除しない。
- Pasteは1行1absolute path。blankを除きcase-insensitive dedupeする。
- Openは現在setをscanする。
- Change folderは現在setを保持してLanding編集へ戻る。即replaceしない。
- Last folder setと最大12件のrecent setを表示する。
- recent shared fileがmalformed/future versionなら上書きせずrecoverable statusを出す。

### WPF-LND-003 Scan

- 各rootをrecursive enumerationする。
- folder/file pathはWindowsとfilesystemが許容する範囲で、日本語、emoji、空白、apostrophe、mixed case、従来の260文字を超えるabsolute pathを文字列加工やshell quoteへ落とさず扱う。folder set、catalog、search結果、preview/modal/tab、Explorer payload、Recycle guardまで同じabsolute path identityを保ち、case差だけの同一rootは重複scanしない。
- 複数rootの一部がmissing、切断、permission変更等で利用不能でも、利用可能なrootの画像はcatalogへpublishする。明示folder setの順序と利用不能rootはcurrent state/Recentに保持し、recoverable statusでskipを通知するため、Refreshで同じsetを再試行できる。成功runだけがstate/Recentを各1回所有し、cancel/stale runは所有しない。
- 利用者が明示したroot自体はscan対象にできるが、その配下で見つけたjunction、symbolic link、mount point等のreparse file/directoryはcatalogへ入れず追跡しない。各rootのlexical tree外をcatalogへ入れず、visited directory setで循環を有限時間に止め、skip件数をrecoverable statusで知らせる。
- enumeration後のexistence snapshotとcatalog materializeの間にsourceが外部削除、rename、切断、access拒否になっても、該当fileだけをrecoverable skipにする。UI dispatcherを落とさず、warning、公開件数metrics、最後の有効selection、Preview tab、Modal、persisted UI参照をsurvivor catalogへreconcileする。source、Favorite、Seen、Recent、Enhancement jobsを副作用で変更しない。
- permission/individual file failureは可能な項目を継続し、summary warningを出す。
- unavailable/access/boundary skipまたはpersistence refusalとdecode failureが同時に存在する場合、後から判明したdecode warningで先の復旧手順やRetry actionを上書きしない。1つのrecoverable statusに両方を残し、利用不能rootを保持したRefresh再試行、保護されたshared state、存在するdecode不能画像のplaceholder継続を同時に説明する。
- supported extension以外をcatalogへ入れない。
- recursive enumerationと、全pathの軽量catalogを公開する前のpre-publication preparation中だけ`Cancel scan`を表示・有効化する。cancelはcurrent generationだけを無効化し、100ms以内を目標にLandingへ戻る。ordered folder draftを保持し、progressを0へ戻し、polite live statusでcancelを通知し、`Open folder set`へfocusを戻す。double cancelはno-op。
- cancelされたrunはpartial catalog/current folder/state/recent/Seenをpublishしない。直前に公開済みのcatalogがある場合はそれを保持し、source、Favorite、Seen、Enhancement jobsを変更しない。遅延完了してもViewerへ遷移せず、直後に開始したnewer scanのcatalog、metrics、stateを上書きしない。
- 全path catalogの公開とViewer遷移が完了した時点でblocking scanは完了とする。dimension/PNG prompt metadataはViewer上のbackground streamであり、`Cancel scan`を出して閲覧をLandingへ戻さない。Change folder、Refresh、closeは新しいgeneration/CancellationTokenで古いmetadataを内部cancelし、stale resultを現catalogへapplyしない。
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

Decoderが個別画像を読めない場合、その画像だけplaceholder/recoverable warningにし、catalog全体を落とさない。read-only sourceは通常表示し、exclusive lock中またはcorrupt sourceは存在する限りcatalogへ残してplaceholderを出す。lock解除/修復後のRefreshで同じpathを再decodeし、成功時は通常previewへ復帰する。format verifierは実fixtureを生成し、decode可否、fallback、他画像の継続を確認する。

表示開始後にsourceがtruncate/corrupt/exclusive lockへ変化した場合も同じ契約を適用する。Right PreviewまたはModalが先に表示したthumbnail/full bitmapを、後続のcurrent full decode失敗後まで成功画像として残してはならない。current path/generationの失敗だけがそのsurfaceをplaceholderへ戻してrecoverable statusを出し、cancel/stale completionは新しいsurfaceを消さない。同じpathが正常画像へreplaceまたはremove/recreateされた場合、次のdecode/Refreshはnew bytesを表示し、old completionを捨てる。

巨大画像と極端な縦横比もsource dimensionのまま無制限にWICへ展開しない。`DecodePixelWidth`は小さいsourceを拡大し得るため、header dimensionからno-upscaleのfitを計算し、surface requested width²×5のpixel budget（絶対上限10,000,000 pixels）とrequested width×8のlong-edge（絶対上限16,384 pixels）の小さい方へaspect ratioを保ってdecodeする。幅が1px未満になる極端な縦長sourceは`DecodePixelHeight`でlong edgeを直接制限する。現在の呼び出し値はPreview tab hover 360、Grid 180〜520、Right Preview 900、Modal 1400で、通常画像の既存表示品質を保ちながら圧縮率の高い縦長画像による巨大allocationを防ぐ。decodeはbackground/cancel可能、path/generation guardは同じで、source bytesとEnhancement queueを変更しない。

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
- GridとListの`ItemsSource`は、先頭だけを切り出したwindowではなく`_tiles`全件を参照する。全件のpath identityとscroll extentは最初のcatalog publishで確定する。
- `VirtualizingWrapPanel`は全件の仮想row geometryだけを持ち、実際のWPF containerはvisible rows + 2 overscan rowsへ限定する。末尾へ到達するための`Load more`、batch追加、384件capを持たない。
- search、selection、modal order、Delete neighborはvisual container数ではなく全件query orderを使う。

### WPF-CAT-002 Realization

- Gridはpixel-scroll対応`VirtualizingWrapPanel`、Listはrecycling `VirtualizingStackPanel`を使う。
- Grid containerはvisible rows + 2 overscan rowsだけをrealizeし、通常上限512件を越えない。これは全件sourceの上限ではなく、同時に存在するvisual containerの安全上限である。
- Gridのscrollbar extentは起動直後からfiltered/sorted全件を表す。drag、PageDown、programmatic selectionのいずれでも末尾へ直接到達できる。
- Created new / Created oldでは、同じvirtual panelが日付headerとcard rowのgeometryを計算・描画する。100,000件分のGroupItemや二次collectionを生成しない。
- Listはvisible containerからviewport内を最優先、その周辺を次点としてthumbnailをscheduleする。Gridから遠方indexへ切り替えても、先頭から順にdecodeし直さない。
- containerを外してもcatalog/selection/Favorite/Seenを失わない。
- 5,000件を常用integrated gate、20,000件をaggregate stress gate、exact 100,000件/100 foldersをlarge-catalog closeout gateとし、欠落、重複、order drift、silent truncateを起こさない。
- exact 100,000 gateはcatalog/filtered/Grid ItemsSourceがすべて100,000、末尾visible indexが99,999、Grid/List/Created/Modalのcanonical pathとselected visualが一致し、全extentを持つことを要求する。fixture実測時間/working set/GCは記録するがhardware非依存の絶対時間だけで合否を作らない。
- far-tailのprogrammatic selectionは全件order上のcanonical pathを正本とする。Grid/List切替でbounded Grid window内indexをfull List indexへ転用せず、Grid→List→Gridの各surfaceで同じpath、`SelectedItems`、realized containerのselected visualを維持する。container生成後のvisual同期はcurrent selection generation/pathだけへ適用し、focus移動、再scroll、全件container探索を行わない。

### WPF-CAT-003 Decode concurrency

- root enumeration worker最大4。重複pathはcase-insensitiveに1件へ統合する。
- metadata worker最大4。ただし10,000件以上は2 workerへ抑え、visible thumbnailとUI操作を優先する。
- thumbnail worker最大12。
- 軽量catalogを先にpublishしてViewerへ移り、dimension/PNG prompt metadataは250ms以上遅延したbackground streamで反映する。metadata全件完了を「一覧を開ける」条件にしない。
- scanで列挙済みのpathへcatalog publish前の全件`File.Exists`を重複実行しない。`FileInfo` materialize時の例外guardとpublish直前の存在snapshotで、列挙後に消えたsourceは従来どおり除外する。
- background PNG indexは同じbounded pre-IDAT streamからIHDR寸法と`parameters` tEXtを1回で読み、返却後に使わない全件metadata dictionaryを保持しない。複数の`parameters` chunkがある時はPreview/Modal readerと同じく最初のchunkだけを採用し、最初が空でも後続へfall throughしない。viewport thumbnail decodeが走っている間はbulk metadata readを待機し、scroll中の可視pixelを優先する。
- background indexの完了snapshotはWPF専用のderived binary indexへ永続化する。既定pathはWPF stateと同じowner directory配下の`metadata-index-v1`で、normalized folder-set hashごとに1 fileとする。BrowserのFavorite、Seen、Recent、thumbnail cache、Enhancement jobsおよびsource画像をindexの所有対象にしない。
- entryはabsolute source path、length、last-write UTC、creation UTC、dimension、検索用Promptを持つ。hitは全source identity一致時だけ許し、1 fileだけ変わった時はそのentryだけsourceから再取得する。Preview/Modalの完全なPNG Settings読取は従来どおりactive imageのlazy readerを正本とする。
- file headerはmagic、schema version、entry count、payload length、SHA-256 checksumを持ち、entry/path/prompt/payloadへ上限を設ける。checksum不一致、truncation、malformed length、invalid UTF-8はexceptionをUIへ漏らさずInvalidとしてsource fallbackする。認識できるfuture versionは読まず、commit時にも再確認してdowngrade overwriteしない。
- 書込みはfolder-set単位writer lock、同一directoryのunique temp、flush-to-disk、atomic replaceで行う。current generationの全source metadataが揃った時だけcommitし、decode failure、catalog mutation、supersession、close、cancelは直前のcomplete indexを保持する。available rootから消えたsourceは次のcomplete snapshotでpruneし、一時的にunavailableな明示rootの既存entryは再接続のため保持する。
- restart後のexact all-hitはindex bytes/mtimeを書き換えず、100,000 entry snapshotの再構築も行わない。Viewerはcatalog公開後も閲覧可能なまま、sidebarとsidebar非表示時のfooterへmonotonic progressを表示し、完了時にreused/refreshed/incomplete/protectedを区別する。
- Browserのversioned thumbnail cacheはread-onlyでprecise `mtimeMs` keyを先に、整数mtime fallbackを次に照合する。cache missまたはdecode failure時だけsourceをbounded decodeし、WPFからBrowser cacheを削除・上書きしない。
- current selection/previewをbackground thumbnailより優先する。
- decode resultはpath/generationを照合し、古いcompletionが新selectionを上書きしない。
- corrupt画像のfailureは他decode queueを止めない。

### WPF-CAT-004 Search responsiveness

- TextChangedから150ms debounce。
- immutable catalog snapshot上でfilter/sortをbackground計算する。
- generationとCancellationTokenでstale queryを破棄する。
- 最終queryだけがUI、selection、preview、stateへapplyされる。
- 100,000件をrapid queryしてもstale queryをdiscardし、最終末尾query/selection/modalがexactで、dispatcher heartbeatが継続する。

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

Foldersは既定展開で、header buttonから折り畳む。collapse中のchildは非表示かつTab対象外。headerはUI Automationの`ExpandCollapse` patternと実際のExpanded/Collapsed stateを公開し、Automation actionも通常clickと同じ経路で状態を更新する。展開/折り畳み状態はstate version 2へ保存し、version 1またはfield欠落時は展開へmigrationする。

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

### WPF-SRC-002 Shared Search History

- Search inputのfocus/clickでshared fileを毎回background threadから再読し、UI dispatcherでdisk I/Oまたは最大2秒のlock待ちをしない。
- popupはRecent searches、個別Delete、Clear allを持つ。履歴選択はtag追加ではなくwhole-query replacementで、WPFの通常SearchQuery保存とは分離する。
- inputのArrowDown/Upで先頭/末尾へ入り、ListBox内Arrowで移動、Enterで適用、Escapeで閉じる。loading、件数、selection、protected/Busy/failureをUI Automation live announcement/statusへ出す。
- 選択後はpopupを閉じてSearchへfocusを戻すが、そのfocus returnで即再表示しない。
- documentはversion 1、MRU `entries`最大50、unknown root field保持。queryをcomma trim/rejoinし、NFKC + Browser/.NET共通code-point foldでdedupeする。trim対象はBrowser/.NET runtime任せにせず、Unicode White_SpaceとU+FEFFの共通表で固定する。
- 1 queryはraw入力とcomma正規化後の双方をUTF-16 code unitで32,768以下に制限する。境界値32,768は保存でき、rawが境界内でも正規化後に超過するcomma-heavy入力は既存fileを変えず拒否する。
- Browserと同じ`<target>.lock`、lock内latest read、30秒stale recovery、temp + atomic replaceを使う。malformed/futureは既存bytesを保護し、Busy/timeoutはcurrent queryとfileを変えない。cross-runtime verifierは両workerのready barrierを解放してwrite区間が実際に重なったことまで証明する。

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
- runtimeと新規writeの`DatePreset`は`none|manual`だけを許す。
- legacy Today/7d/30d/year tokenは起動互換readerだけで受ける。既存DateFrom/DateToがあればpartial endpointを含めexact保持し、無ければload時点の対応rangeを一度だけ固定して`manual`へ書き戻す。以後、日付が進んでも相対rangeとして再計算しない。
- migrationはunknown extension fieldを保持し、UIへpresetを復活させない。

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
- Ctrl+Aはcurrent filtered resultの全real-file path、Ctrl+Shift+Aはclear。canonical selectionはpath setで保持し、100,000件でもWPF `SelectedItems`へ全件をmaterializeしない。大量選択のvisual stateはrealized/recycled containerだけへ投影し、container generation/scroll時に再同期する。
- Grid/Listは同じselected path setを共有する。
- background clearは明示空白clickだけ。
- virtualization window更新中はselection eventsをsuppressし、selected setを消さない。
- 複数選択時はright panelをbulk surfaceへ切り替える。
- Grid/ListのEnterはcurrent filtered/sorted orderのprimary imageをModalで開く。Modal navigation後に閉じた場合もcurrent primary Grid/List itemへfocusを戻す。Search、Date、Settings、Delete、Modal native input、LandingのEnterはgalleryへ漏らさない。button focusのEnter/Spaceはnative activationを1回だけ行う。

### WPF-GAL-003 Favorite操作

- F: selected image(s)を+1、最大5。
- U: selected image(s)を-1、最小0。
- single actionはcurrent primaryへ適用。
- bulk surfaceはLv0〜5 exact set、Increase all、Decrease allを持つ。
- multi-selection summaryは件数とmixed/uniform levelを表示する。
- bulk writeは1回のshared merge transaction。途中の画像ごとにfileを書かない。
- disk latestの無関係path、unknown entryを保持する。
- malformed/lock timeout/write failureはmemory/diskをrollbackし、selectionを保持する。
- 1 MiB以上のshared storeはgeneration-aware single-writer actorへ渡す。UIはoptimisticに更新するが、成功はlock内latest-disk mergeとatomic replaceの完了後だけdurableとする。failureは同じgenerationだけrollbackし、後続generationを巻き戻さず、Retryを残す。

### WPF-GAL-004 Seen/Unseen

- explicit selection、right preview表示、modal open/navigationでSeenをadditiveに付ける。
- scroll/realize/thumbnail decodeだけでは付けない。
- Unseen dots既定OFF。ONは未Seen itemへdotを表示するだけ。
- toggleで`.cache/seen.json`を消去・false化しない。
- shared writeはlatest diskとのunion、process lock、temp+replace。malformed fileは上書きしない。
- 1 MiB以上のshared storeはFavoriteと独立したsingle-writer actorで保存する。同じpathの連続Seenはcoalesceし、外部writerのentryを保持し、failureは同じgenerationのdot/filterだけを元へ戻す。
- folder reloadがFavorite/Seen writerをdrainしてdisk snapshotを再読込する短いtransaction中は、新しいFavorite/Seen操作を受理せずrecoverable statusを出す。これにより一方のwriter待ち中に受理した操作が後続disk reloadで消える競合を作らない。

### WPF-GAL-005 Gallery zoom

- range 20〜600、default/reset 200、step 20。600 endpointはavailable widthに関係なく正確に1列へ固定する。
- 既存の40〜600保存値は値を変えずに継続利用し、20未満、600超、非有限値だけを安全にclamp/migrateする。unknown fieldは保持する。
- Ctrl+wheel、Ctrl+`+`、Ctrl+`-`、Ctrl+`0`。
- Ctrl/Win+wheelはViewer galleryがactiveで、Settings/Delete overlayがなく、pointer/focus sourceがTextBox、ComboBox、DatePicker、Buttonでない時だけgallery zoomへ渡す。Landingや入力control上ではnative wheelを維持しcard widthを変えない。
- zoom前にviewport基準画像のcanonical full pathとviewport offsetをcaptureする。visible selectionがあればそれを優先し、なければviewport centerを使う。
- reflow後に同じcanonical pathをrevealしoffsetを補正、scroll boundsへclampする。別folderの同名fileをfile nameだけで同一視しない。
- zoom、Sidebar開閉、right panel resize、window resize、DPI changeで同じpath/offsetを復元する。selection有無の両方を対象とする。
- 20→600→200と旧40保存値のfixtureでanchor driftを1 card未満、現行smokeでは0に保つ。600時もrealizationをboundedにする。
- sidebar/header/font/right panel/List row sizeは変えない。List mode中のgallery zoom操作は拒否し、recycling virtualizationを維持する。

### WPF-GAL-006 Explorer FileDrop drag-out

- Grid、List、right preview imageでleft pointerがWindows system drag thresholdを越えた時だけ開始する。threshold未満のclick/double-click/selection/modal openを奪わない。
- drag originがpointer-down前のextended selectionに含まれる時は、current display orderのselected real filesをまとめて渡す。含まれない時はorigin 1件だけを渡す。
- payload作成直前に全pathをcanonicalizeし、active root内、current catalog membership、supported image extension、actual file existenceを検証する。1件でも不正ならFileDropを開始しない。
- `DataFormats.FileDrop`の`string[]`を`DragDropEffects.Copy`でExplorer等へ渡す。Preview tab reorderとModal pan/swipeは別surfaceで競合させない。
- Right PreviewとModalはNATIVE-EXTENSIONのShow in folderを持つ。active catalog/root/type/existence/canonical guard後だけ`explorer.exe`へ正確に1件の`/select,<canonical path>`を`ArgumentList`で渡す。shell command文字列や手組みquoteは使わず、copy/move/delete/state変更はしない。
- Modalの`Open externally`/Enterは同じactive catalog/root/type/existence/canonical guardを起動直前に再実行する。Original表示中はsource、Enhanced表示中はsource identity/signatureとmanaged ownershipを満たすsucceeded outputを`UseShellExecute`へ1件だけ渡す。missing/stale/invalid signature/ownership外EnhancedはOriginalへfallbackして明示statusを出す。未関連付け、ShellExecute拒否、I/O/access/path failure、起動直前の消失はUI dispatcherへ逃がさずgeneric Retry statusへ変換し、selection、Modal、focus、source/shared stateを変更しない。
- `ArgumentList`と`DataFormats.FileDrop`はUnicode、emoji、空白、apostrophe、long absolute pathを1件のpathとしてexactに保持する。検証ではExplorer/dragを実起動せずinjected launcher/payloadだけを観測する。
- invalid/0件はrecoverable status。成功/失敗のどちらでもsource、state、Favorite、Seen、Enhancementを変更しない。
- Grid/List/right previewはAutomation HelpTextとtooltipでdrag-outを説明する。

### WPF-GAL-007 Explorer folder drag-in

- Landing全体とViewer galleryをdrop targetにし、valid folderがdrag-over中だけ既存Accent tokenのoverlayで「folder setへ追加・copy/moveなし」を示す。
- `DataFormats.FileDrop`から既存absolute directoryだけをcanonical absolute pathへ正規化する。file、relative path、missing path、canonicalization failureは拒否理由をstatusで示し、valid itemがあればそれだけを受理する。
- folder setの順序を保ち、case-insensitive duplicateは追加しない。Explorerへのimage drag-outはfile-only payloadのためfolder affordanceを出さず、copy payloadの既存契約を変えない。
- Landingはfolder-set draftのみ、Viewerは既存setへappendしてscanする。どちらもsource、Favorite、Seen、Enhancement jobを直接変更しない。
- drop targetはAutomation Name/HelpTextを持つ。keyboard Add/Change/paste導線は維持する。

## 9. Right preview

- 既定幅340、範囲240〜900。
- splitter mouse dragとkeyboard incrementに対応する。
- widthとopen/closed stateを保存する。
- single selection: image、filename/path、Favorite level/actions、Open、Open tab、metadata、source Delete。
- multi selection: count、level summary、bulk Favorite、bulk Recycle。
- selectionなし: image placeholder/action/metadataを隠し、`Click an image to open preview.`のempty guidanceだけを表示する。
- preview decodeはimmediate placeholder + background decode。generation/path guardでstale completionを捨てる。
- immediate thumbnail表示後にcurrent full decodeが失敗した場合は、そのthumbnailをclearしてplaceholder + recoverable statusへ移る。source修復後のRefreshで同じpathを再decodeする。
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
- scan後のcurrent catalogに存在するpathだけを同じorderで復元する。current search、Favorite/Unseen、folder/date filterの表示集合ではなくfull catalogを基準にし、filter外tabとpinを失わない。
- activeが欠落なら先頭へfallbackする。
- reload restoreだけでmodalを開かない。
- closeしてもpin markerは残し、同じpathの再open時に復元する。
- async searchまたはclient filterでactive tabが表示集合外になってもopen tab/pinはfull catalog基準で保持する。active markerはcurrent selectionを偽って残さず、filter解除後に同じtabを再activateできる。
- filter確定がModal表示と競合した時はModalを新しいfiltered primaryへ同期する。result 0件ならstale image/actionを残さずModalを閉じ、Modal内focusだった場合はSearchへ戻す。

### WPF-TAB-003 External source lifecycle

- Refreshでsourceが外部削除またはrenameされてcurrent scanned rootから消えた場合、そのold pathをopen/active/pinned tab、selection、right preview、Modal、persisted UI参照から除去する。Favorite/Seen/Enhancement historyは別ownershipのため削除しない。
- current catalogに残るclosed tabはpathで新しいTileへrebindし、Refreshによるobject identityの更新だけでReopen historyを失わない。消えたpathだけをclosed stackから除去する。
- missing sourceを表示中のModalは、選択fallbackがあれば同じModal surfaceで新selectionへ同期し、Modal内focusを保持する。catalogが空ならModalを閉じ、Open folder setへfocusを戻す。
- decode不能だが存在するsourceはcatalog/Favorite/Seen履歴を維持し、Right PreviewとModalをplaceholder + recoverable warningにする。
- 表示中sourceがtruncate/lockへ変化してfull decodeに失敗した場合、Right Preview/Modalは以前の同一path bitmapを残さない。同名replace/remove-recreate/Refreshではlatest generationのbytesだけをapplyする。
- rename後のnew pathは推測でold Favorite/Seen/Enhancement identityを継承しない。

### WPF-TAB-004 Hover performance

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
- zoom 0.25〜10、wheel/keyboard、reset。wheel zoomはimageまたは空のimage-area由来だけを消費し、metadata sidebar、top bar、edge/button/footer由来は消費せずnative scroll/control処理へ渡す。
- pan、horizontal flip。
- Original/Enhanced表示切替。成功済みmanaged outputだけを候補にする。
- 現在表示中assetの実体容量を、bytes / 1024²、小数2桁、spaceなしのexact `0.00MB`形式で表示する。Originalはsource bytes、Enhancedはmanaged output bytesで、toggle時に即更新する。Enhancedがmissing/stale/invalid signature/ownership外ならOriginal容量とrecoverable statusへfallbackする。
- Prompt / Negative / Settings metadata tabs、copy。
- Escapeで閉じ、元selectionへfocus/contextを戻す。
- Delete成功後はpre-delete orderのnext、なければpreviousへ継続。最後の1枚なら閉じる。
- zoom 1.0でdrag閾値未満のimage single-clickはmanual chromeをhide/showする。manual visibleは操作停止でも消さない。manual hiddenではcursorを隠し、pointer/keyで約900msだけ一時表示して戻す。control clickはchrome toggleへ伝播しない。
- image double-clickはmetadata sidebar toggleを優先し、遅延single-click chrome toggleをcancelする。
- image areaのleft 28% / center 44% / right 28%をpointer zoneとする。left/rightはprevious/next、centerはimage chrome toggleで、各zoneはAutomationName/HelpTextを持つ。黒いbackdropは一覧へ戻し、topbar/footer/control clickはbackdropやchrome toggleへ伝播しない。
- zoom 1.0で水平移動が垂直移動を上回り、画像幅16%を72〜180pxへclampした閾値を越えたpointer gestureはprevious/next swipe。zoom > 1ではpanだけを行う。
- previous/next、zoom/reset、chrome hide/showは650msのtransient feedbackを既存accent/glass tokenで示す。manual hidden状態はprevious/next、Delete隣接移動、Original/Enhanced切替後も維持する。
- filtered/sorted full orderのFilmstripを持つ。manual chrome表示中は画像外の専用bottom rowを確保してviewportを縮める。manual hidden中にpointerが下端128pxへ入った時は前面overlayとして表示し、離脱時に畳んで画像geometryを変えない。currentへ追従し、clickで直接選択する。`T`/toolbarで開閉し、`ViewerState.ModalFilmstripOpen`へ保存する。
- zoom倍率はModal root最上端の低contrast indicatorへ表示し、画像・topbar・Filmstripへ重ねない。
- Modal top buttonへfocus中もArrow/Delete/T/H/F等のviewer shortcutを処理する。Enter/Spaceはfocused buttonのnative activationへ渡し、TextBox/ComboBox/DatePickerはviewer shortcutを抑止する。
- Modal Enter external openは容量表示と同じdisplayed-asset resolverを使う。canonical source/signature/managed ownershipを再検証し、ShellExecute failureではModalを閉じずRetry可能なstatusを残す。
- Prompt metadataはcomma区切りをtrimし、emptyとcase-insensitive duplicateを除外したstable-order chipとして表示する。
- Modalのimmediate preview/thumbnailはfull decode成功まで暫定表示である。current full decode失敗時はclearしてplaceholder + recoverable statusへ移り、stale/cancel resultは最新Modalを変更しない。
- Prompt chipはclick/Enter/Spaceで既存comma queryへ重複なくappendし、modalを閉じ、filterを適用し、Searchへfocusを移す。queryは通常stateとしてreload後も復元する。
- chipはAutomationName/HelpTextを持つ。promptなし時は空白面にせずfallbackを表示する。この操作でsource/metadata/Favorite/Seen/Enhancementを変更しない。

## 12. Source Delete / Recycle

### WPF-DEL-001 Guard

Delete直前に毎回すべて再検証する。

1. pathをcanonicalizeできる。
2. current active root配下。
3. current catalog/indexに存在。
4. supported image extension。
5. actual fileが存在。
6. lexical pathまたはcanonical pathがrepository/project root、`AppContext.BaseDirectory`のapp rootへ入らない。active rootとして明示scanされていても拒否する。
7. active root内のreparse pointからproject/app rootや別rootへescapeしない。

### WPF-DEL-002 Execution

- Windows Shell Recycle Binだけを使う。
- hard delete API、`File.Delete` fallbackを持たない。
- confirm既定ON。
- confirmationはCancel、Recycle、Do not ask againを持つ。
- Recycle成功を確認してからcatalog/stateを更新する。
- failure/cancelではsource、catalog、Favorite、Seen、selection、tabsを変えない。
- Refreshがenumeration/metadata中にRecycle成功した場合、そのRefreshが先にcaptureしたold sourceをpublishしてはならない。successful pathだけをload-start generationより新しいtombstoneとして扱い、post-Deleteのneighbor selection、preview、Modal、focusを維持する。
- tombstoneはfailed/cancel pathへ付けず、次のfresh Refresh開始時に失効する。同じpathへ後から生成された新sourceを永久に隠さない。

### WPF-DEL-003 Neighbor

- action前のcurrent filtered/sorted orderとprimary indexをsnapshotする。
- success後は同じorderのnext、なければprevious。
- filter外の画像へ勝手に飛ばない。
- last itemならpreview/modal/selectionをemptyへ整合する。

### WPF-DEL-004 Bulk

- multi-selectionのsnapshotを対象にする。
- count付きexplicit confirmation。
- 各pathを同じsingle guard/Recycle serviceで独立実行する。
- successful pathだけをcatalog、selection、open/active/pinned/closed preview tab、active preview/modal、persisted UI参照から共通reconciliation helperで除去する。
- Favorite、Seen、Enhancement job/outputはmulti-owner履歴または別削除権限なのでsuccessful pathも保持する。
- failed pathはsourceとUIに残す。
- resultはsucceeded/failed countと復旧可能な理由を表示する。
- all-or-nothingを偽装しない。hard-delete fallbackは0回。

実ファイルtestは必ず専用temp copyだけを使う。

## 13. Enhancement isolation

- WPFはshared `.cache/enhance/jobs.json`をreadし、succeeded jobのmanaged outputをsourceへ関連付ける。
- malformed job、missing output、unsupported outputでもsource browsingを継続する。
- scan、selection、preview、tab hover、modal navigationはjobs fileを変更しない。
- worker processを自動startしない。
- Modalの明示`AI x2`だけがloopback Browser APIへjob createを送る。queued/runningはCancel、failed/canceledはRetry、succeeded managed outputはoutput deleteを表示する。source Deleteとは別action・別確認・別endpointである。
- Browser API baseは既定`http://127.0.0.1:3000/`、環境overrideもloopback hostだけを許可する。WPFはBrowser server/workerを起動・停止せず、`jobs.json`へ直接writeしない。Browser engineが未起動、selected sourceがBrowser active index外、またはAPI failureならrecoverable inline statusを出して閲覧を続ける。
- create/cancel/delete/retry応答はmodal generation、selected source path、job source pathを再照合する。応答待ち中に隣画像へ移動した場合、古いresponseを新しい画像へ適用しない。

## 14. Settings、state、persistence

App Settingsはread-onlyのAbout / Diagnosticsを含む。表示とCopy payloadは製品/version、exe build UTC/local time、process architecture、.NET runtime、catalog/visible count、safety flagsだけであり、folder/file path、prompt、metadata、favorites、state/cache内容を含めない。revision/dirtyは`PVU_SOURCE_REVISION` / `PVU_SOURCE_DIRTY`がある時だけ示し、ない時はlocal buildとする。

Unseen dotsはsidebarとApp Settingsの両方に同じ`ShowUnseenDots`設定として置く。どちらから変更しても他方へ即時同期し、既定OFF、stateへ1回だけ保存する。これは表示設定であり、ON/OFF、dialog open/close、reloadのいずれもSeen JSONを追加・削除・再書き込みしない。両surfaceは同じAutomation name/helpを持ち、keyboard focus可能とする。

App SettingsのKey bindingsは、実装済みactionだけを1行ずつ表示し、現在のdraft chordを確認、capture、競合修正、既定値へreset、保存できる。capture中のmodifier単独、IME/system処理key、keyなしは拒否し、bare Escapeはcapture cancelとして消費する。keyboard focusを壊すTabの全組合せ、Alt+F4、Alt+Space、Ctrl+Alt+Delete（追加modifier付きも含む）、Ctrl+Shift+Escape、Ctrl+Escape、Alt+Escape、すべてのWindows-key chordをOS/focus予約としてinline拒否する。Win+Shift+TはOSに先取りされ得るためalternateとして公開・既定化しない。persisted JSONへ直接書かれた同じinvalid chordも採用しない。Resetはdraftだけを変更し、Save成功時に初めてruntimeへ即時反映する。保存拒否または書込み失敗では現在のactive mapを維持し、編集draftを失わない。

Thumbnail status bordersはFavoriteを既定ON/`#facc15`の2px inner、AI-enhancedを既定ON/`rainbow`の3px outerとして独立設定する。`60ed739`以後、WPFは変更したFavorite/Enhanced preferenceだけをlock内latest `.cache/settings.json`へmergeし、別surfaceが同時更新したもう一方を古いdraftで戻さない。dirty preferenceなしの保存はno-opまたは保護中Retry案内で、empty mutationを作らない。unknown field、旧Enhanced hex、malformed/future、fresh busy lockを非破壊で扱う。

同じchordは、Gallery-onlyとModal-onlyのように同時にactiveにならないcontext間だけ再利用できる。Viewer全体と重なるaction、または同じcontext内の重複は行ごとのinline conflictとして示し、解消までSaveを無効にする。App SettingsとRecycle confirmationのbare Escapeは設定外の固定rescue keyであり、誤設定中でもdialogを閉じられる。Modal closeは設定対象で、既定Escapeを別keyへ変えた場合は新しいkeyだけがModal closeを担当する。

### WPF-STATE-001 ViewerState v2

保存field:

- LastFolder / LastFolderSet
- SearchQuery / SelectedPath
- CardWidth
- RightPanelOpen / RightPanelWidth
- DisplayStyle / AspectMode
- SortBy / RandomSortSeed
- DatePreset (`none|manual`) / manual DateFrom / DateTo
- ShowFavoritesOnly / ShowUnfavoriteOnly / FavoriteFilterLevels
- ShowUnseenDots
- ConfirmBeforeDelete
- FoldersSectionExpanded / HiddenFolderBuckets / SelectedFolderBucketKeys / PrimarySelectedFolderBucketKey
- PinnedPreviewPaths
- PreviewTabPaths / ActivePreviewTabPath
- KeyBindings（実装済みaction storage name -> canonical chord。未知のnested entryも保持）
- unknown extension fields

version 1はmissing folder sidebar fieldsをexpanded/emptyへadditive migrationする。旧scalar `FavoriteFilterLevel`はreaderだけ残し、同じexact single levelへ移行する。旧relative DatePresetは具体的なmanual rangeへ一回だけ移行し、次write以降は`none|manual`だけを保存する。KeyBindings欠落は既定mapへ移行する。個別のinvalid chordはそのactionだけ既定値へ戻し、保存map内にactive-context conflictがある場合は曖昧な一部mapを採用せず既定map全体へ安全に戻す。Quick Search/date presetをUI/runtimeへ復活させない。

state保存はlock内でlatest diskを再読込し、KeyBindingsの未知nested entryもlatest-writer優先でmergeする。window open中に外部writerが未知entryを削除した場合はresurrectせず、後から追加した未知entryはvalueを保持する。top-level unknown extension fieldも同様に保持する。

### WPF-STATE-002 Write safety

- UI changeをdebounceする。search/stateは300ms単位。
- targetごとのprocess lock、2秒timeout、25ms retry、30秒stale recovery。
- 30秒以内のfresh lockはowner processの生死やpayload形状に関係なくauthoritativeとし、target bytes、lock、atomic tempを変更しない。30秒を超えたlockだけをcrash orphanとして回収する。
- UI threadのzero-wait writeでも、stale lockを正常に除去した時だけcreate-newをその場で1回再試行し、ユーザーの最初の操作で復旧を完了する。fresh contentionでは待機しない。
- lock内でlatest diskを再読し、unknown fieldとexternal updateを保持する。
- temp fileへwriteしてatomic replace/renameする。
- lock取得直後、同じtargetに属するWPF形式`.{target}.*.tmp`とBrowser形式`{stem}-*.tmp`のcrash residueだけを削除する。他targetのtempは変更しない。
- malformed/future incompatible fileを空stateで上書きしない。
- failure時は既存fileを保持し、recoverable statusを表示する。
- window close時は300ms state debounceを待たず、SearchInputを含む最終viewer stateをUI threadで一度だけflushする。pending search/preview/metadata/modal/tab-hoverをcancelし、stale completionはclose後にstate/UIへapplyしない。
- close flushはviewer stateだけを対象とし、source、Favorite、Seen、Recent、Enhancement jobsを副作用で再writeしない。malformed/future-protected/contended stateでは既存fileと所有中lockを保持したままcloseを完了する。
- shared Recentは明示的なfolder set open/changeが成功した時だけcommitする。同一windowで最後に成功した同一setは再writeせず、search、resize、settings、通常state save、active-folder refresh、closeではbyte-identicalを維持する。失敗したcommitは成功済みと記録せず、次の明示commitで再試行する。

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
| Modal filmstrip | ON |
| Favorite thumbnail border | ON / `#facc15` |
| AI-enhanced thumbnail border | ON / `rainbow` |

## 15. Keyboard、focus、accessibility

Editable key bindingsの既定値:

| Action | Key |
| --- | --- |
| Modal previous / next | Left / Right |
| Modal close / galleryへ戻る | Escape |
| Favorite +1 | F |
| Favorite -1 | U |
| Favorite exact Lv1〜5 | Ctrl + 1〜5 |
| Recycle current primary image | Delete |
| Select all current results / clear selection | Ctrl + A / Ctrl + Shift + A |
| Reopen last closed preview tab | Ctrl + Shift + T |
| Move focused preview tab left / right | Alt + Shift + Left / Right |
| Modal flip | H |
| Toggle Original / already-succeeded Enhanced | E |
| Toggle Modal filmstrip | T |
| Gallery zoom in / out / reset | Ctrl + `+` / `-` / `0` |
| Modal zoom in / out / reset | `+` / `-` / `0` |

Ctrl/Win + wheelのgallery zoomはpointer gestureなのでKey bindingsの対象外だが、sidebar/textのscaleを変えず同じgallery zoom modelを使う。Win chordはeditable key bindingには採用しない。`Toggle Original / Enhanced`は既にsucceededでmanaged outputが存在する時だけ表示sourceを切り替える。key設定にjob create、worker start、暗黙Enhancement actionを追加してはならず、明示`AI x2`だけがSection 13のcreate routeを使う。

Guard:

- TextBox、ComboBox、DatePickerへfocus中はgallery/viewer shortcutを通さない。Button focus中はgallery shortcutを通さないが、active Modal内ではArrow/Delete/T/H/F等のModal viewer shortcutを処理し、Enter/Spaceだけをbutton native activationへ渡す。
- Landing表示中はGallery/Viewer actionを通さず、hidden selection、Favorite、Delete confirmation、preview-tab history、card widthを変更しない。
- configured Modal closeはModal child button上でも到達可能とし、focused preview-tab button上のconfigured tab reorderも維持する。
- Delete/App Settings dialog中はdialog keyだけを優先し、bare Escapeを固定rescueとして保持する。
- modal transform keyはmodal表示中だけ。
- Grid/List EnterはViewer primary itemにだけ適用し、Search/Date/Settings/Delete/Modal native input/Landingでは抑止する。Modal/Prompt chip button上のEnter/Spaceはnative actionを優先し、二重にModalを開かない。
- Ctrl/Shift selectionはnative selection semanticsを壊さない。
- Settingsを閉じた未保存draftは破棄し、Save済みbindingは同processへhot applyして別process/reloadでも復元する。

Accessibility:

- interactive controlはButton/Toggle/CheckBox等のnative peerを使う。
- icon-only controlはAutomationName/ToolTipを持つ。
- dialogはinitial focus、Escape、close後focus returnを持つ。
- Modalはnamed focusable rootへinitial focusを置き、Tab/Control+Tabをsurface内でcycleする。Modal内buttonへfocus中でもEscapeは最上位surfaceを閉じ、明示close後はopener、現在表示中のGrid/List、Landing primary actionの順で有効なfocus targetへ戻す。
- Modal metadata sidebar、Prompt/Negative/Settings tab、copy action、previous/next edge zoneはAutomation Name/HelpTextを持つ。
- bulk summary/errorはscreen readerが理解できる名前/statusを持つ。
- disabled actionは単に消すのではなく、必要なら理由をstatusへ示す。
- keyboardだけでLanding→Viewer→filter→selection→preview→tab→modal→settings→Delete confirmationを操作できる。

## 16. Error、loading、empty state

- recoverable errorはblocking MessageBoxを乱用せず、viewer/landing内statusへ表示する。
- statusはaction、原因、継続可否、retry可能ならretryを示す。
- persistence lock busyはbounded retry後に明示する。
- live/fresh owner lockをstealしない。crash後は30秒stale境界を超えた次の正常writerがlockを回収し、target別atomic temp residueも除去する。
- malformed Favorite/Seen/state/recentは元fileを保持し、書込みを拒否する。
- shared recentの同時書込みは `<target>.lock` create-new lock と atomic replace を使い、Browser/WPF/独立writerの最新folder setを加算的に残す。`lastFolderSet` は単一値のため最後にlockを正常取得したwriterを採用する。
- WPFの明示Recent commitはlock取得後にdisk最新値を再読し、新setを先頭へ追加してdistinct最大12件に制限する。外部ownerのhistoryとunknown fieldを保持し、一般state保存では共有Recentを開かない。
- decode failureは該当画像だけ。
- scan partial failureは成功件数とskipを示す。
- normal zero-result、no supported images、scan failure、filter emptyを別copyにする。
- error後も他画像のselection/preview、folder change、settings closeが可能。

## 17. Verification contract

変更は最低限、影響workflowの専用verifierと次の共通gateを通す。

通常のWPF aggregateは`powershell -File scripts/verify-wpf-product.ps1`で一括実行できる。これは`verify-ui-regression-guard.ps1`と通常の`verify-wpf-*.ps1`を自動検出し、Search Historyのfocused/cross-runtime verifierは順序を固定して明示実行する。日常の短い回帰では`-SkipStress`を使えるが、closeoutでは20,000件stressを省略せず、`-IncludeReloadSoak`で既定24-cycleの同一process soakをaggregate内へ追加し、Favorite/Seen、Recent、Search Historyのcross-runtime gateを通す。large-catalog変更のcloseoutでは、aggregateとは別に`-Count 100000 -FolderCount 100`を必ず1回通す。40-cycle以上は長期memory傾向のextended観測として必要時に別実行する。check件数はscript欠落を見つけるsnapshotであり固定合格件数ではないため、追加verifierを削って数を合わせず、live JSONの`checks`と全resultを記録する。

| Area | Command |
| --- | --- |
| Retired UI regression guard | `powershell -File scripts/verify-ui-regression-guard.ps1` |
| P0 integrated / 5,000 | `powershell -File scripts/verify-wpf-p0.ps1` |
| Browser/WPF shared Favorite/Seen concurrent stress | `powershell -File scripts/verify-cross-runtime-shared-state.ps1 -Iterations 20` |
| Browser/WPF/third-writer shared Recent concurrent stress | `powershell -File scripts/verify-cross-runtime-recent.ps1 -Iterations 20` |
| Shared Search History UI/schema/trim/length/protection | `powershell -File scripts/verify-wpf-search-history.ps1` |
| Browser/WPF shared Search History barrier-overlap concurrent stress（lossless上限23） | `powershell -File scripts/verify-cross-runtime-search-history.ps1 -Iterations 20` |
| Catalog aggregate stress / 20,000 | `powershell -File scripts/verify-wpf-catalog-stress.ps1 -Count 20000` |
| Exact large catalog / 100,000・100 folders | `powershell -File scripts/verify-wpf-catalog-stress.ps1 -Count 100000 -FolderCount 100 -OverallTimeoutSeconds 180` |
| P1 search/date/folder | `powershell -File scripts/verify-wpf-p1a.ps1` |
| Date preset retirement/migration | `powershell -File scripts/verify-wpf-date-filter.ps1` |
| P1 error/a11y/state | `powershell -File scripts/verify-wpf-p1b.ps1` |
| Smoke process/environment isolation | `powershell -File scripts/verify-wpf-automation-isolation.ps1` |
| Formats | `powershell -File scripts/verify-wpf-formats.ps1` |
| PNG parameters first-chunk consistency | `powershell -File scripts/verify-wpf-png-metadata.ps1` |
| Persistent prompt metadata index / restart / corruption / cancellation | `powershell -File scripts/verify-wpf-metadata-index.ps1` |
| Oversized/high-aspect decode bounds | `powershell -File scripts/verify-wpf-decode-bounds.ps1` |
| Right panel | `powershell -File scripts/verify-wpf-right-panel.ps1` |
| Bulk Favorite | `powershell -File scripts/verify-wpf-bulk-favorite.ps1` |
| Gallery zoom / one-column / geometry anchor / state migration | `powershell -File scripts/verify-wpf-gallery-zoom-anchor.ps1` |
| Search responsiveness | `powershell -File scripts/verify-wpf-search-stall.ps1` |
| Preview tabs/reload | `powershell -File scripts/verify-wpf-preview-tabs.ps1` |
| External delete/rename/decode recovery | `powershell -File scripts/verify-wpf-external-stale-source.ps1` |
| In-place corrupt/lock/replace/recreate/Refresh decode race | `powershell -File scripts/verify-wpf-decode-mutation.ps1` |
| Rapid UI/final-state stress | `powershell -File scripts/verify-wpf-rapid-ui-state.ps1` |
| Filter/focus/selection dispatcher race | `powershell -File scripts/verify-wpf-focus-filter-race.ps1` |
| Folder switch/Refresh same-process soak | `powershell -File scripts/verify-wpf-reload-soak.ps1 -Count 1000 -Cycles 40` |
| Shutdown final-state lifecycle | `powershell -File scripts/verify-wpf-shutdown-state.ps1` |
| Runtime/version safe diagnostics | `powershell -File scripts/verify-wpf-diagnostics.ps1` |
| Shared Recent write ownership | `powershell -File scripts/verify-wpf-recent-write-ownership.ps1` |
| Process crash/live lock/stale recovery | `powershell -File scripts/verify-wpf-crash-lock-recovery.ps1 -Iterations 3` |
| Partial multi-root scan / ownership | `powershell -File scripts/verify-wpf-partial-scan.ps1` |
| Scan existence-to-materialization race recovery | `powershell -File scripts/verify-wpf-scan-materialization-race.ps1` |
| Explicit scan cancellation / supersession | `powershell -File scripts/verify-wpf-scan-cancel.ps1` |
| Recursive scan boundary | `powershell -File scripts/verify-wpf-scan-boundary.ps1` |
| Unicode/long path/lock/corrupt/mixed-root lifecycle | `powershell -File scripts/verify-wpf-path-robustness.ps1` |
| Current-monitor maximize/topology-safe restore | `powershell -File scripts/verify-wpf-monitor-work-area.ps1` |
| Preview hover | `powershell -File scripts/verify-wpf-preview-tab-hover.ps1` |
| Preview tab reorder | `powershell -File scripts/verify-wpf-preview-tab-reorder.ps1` |
| Modal wrap | `powershell -File scripts/verify-wpf-modal-wrap.ps1` |
| Modal pointer/chrome | `powershell -File scripts/verify-wpf-modal-interaction.ps1` |
| Modal explicit Enhancement actions / stale response isolation | `powershell -File scripts/verify-wpf-modal-enhancement-actions.ps1` |
| Modal prompt tag to search | `powershell -File scripts/verify-wpf-prompt-tag-search.ps1` |
| Explorer FileDrop drag-out | `powershell -File scripts/verify-wpf-file-drag-out.ps1` |
| Explorer Show in folder | `powershell -File scripts/verify-wpf-explorer-reveal.ps1` |
| Guarded external app open / failure recovery | `powershell -File scripts/verify-wpf-external-open.ps1` |
| Grid/List Enter → Modal / current-order focus return / input isolation | `powershell -File scripts/verify-wpf-gallery-enter-modal.ps1` |
| Explorer folder drag-in | `powershell -File scripts/verify-wpf-folder-drag-in.ps1` |
| Bulk Recycle | `powershell -File scripts/verify-wpf-bulk-recycle.ps1` |
| Delete protected roots / ownership reconciliation | `powershell -File scripts/verify-wpf-delete-correctness.ps1` |
| Delete decode/Refresh race / tombstone lifetime | `powershell -File scripts/verify-wpf-delete-race.ps1` |
| Folder bucket selection/persistence | `powershell -File scripts/verify-wpf-folder-buckets.ps1` |
| Sidebar/App Settings Unseen dots synchronization | `powershell -File scripts/verify-wpf-settings-unseen-dots.ps1` |
| Editable key bindings / wheel・Landing isolation / 100k selection / hot apply / two-process reload / reset | `powershell -File scripts/verify-wpf-key-bindings.ps1` |
| Shared Favorite/Seen writer fault/coalescing/close contract | `powershell -File scripts/verify-wpf-shared-state-writer.ps1` |
| Large shared Favorite/Seen latency and exactness gate | `powershell -File scripts/verify-wpf-shared-state-latency.ps1 -Repetitions 3` |
| Exact visual viewports / Browser comparison states | `powershell -File scripts/verify-wpf-visual-layout.ps1 -EvidenceDir <path>` |
| Direct launcher exact provenance / fail-closed rebuild / success no-pause | `powershell -File scripts/verify-wpf-launcher-freshness.ps1` |

Reload soakのmemory correctness gateは、warm-up直後と終了時のforced-GC managed heap差が128 MiB以内、終了working set差が512 MiB以内、peak working set差が768 MiB以内であることを要求する。Windows/WPFのworking setにはnative allocator、WIC/render cache、OS trim timingが含まれ、短い24-cycleの回帰直線だけでは保持中の資源と後から解放されるcacheを区別できない。そのため`WorkingSetPlateauObserved`、tail slope/decrease、post-warm slope、managed差を含む全sampleは結果へ残すが、plateau観測単独はcorrectness gateにしない。実リークはforced-GC後のmanaged上限とworking-set絶対envelopeで停止させ、長期傾向は40-cycle以上の結果を比較して判断する。

共通pass条件:

- Release build 0 error。
- `start_wpf.bat`はproject rootをworking directoryにし、Release exe hashをrepo root、project/target path、git revision、WPF source fingerprintへ結び付けたatomic provenanceが一致する時だけdirect launchする。missing/invalid/wrong-worktree/wrong-revision/source drift/target改変はbuildへ戻り、build/provenance失敗時は旧binaryを起動しない。Node/localhostへ依存せず、既存WPF processをkillしない。
- disposable fixture以外をDeleteしない。
- source count、catalog count、filtered order、selection、previewが期待通り。
- existing state/cacheを削除しない。
- Browser route workerとWPF writerを同じtemp Favorite/Seenへ20回並走し、別path 40件をlost updateなしで保持する。valid JSON、lock/tmp residue 0、real port/user cache非使用を確認する。
- Favorite、Seen、Recent、ViewerStateごとにactual WPF processをabrupt exitさせ、fresh lock不変、31秒相当stale回収、最初のUI-thread write成功、unknown field保持、malformed/future拒否、target別temp cleanupを反復する。同時にlive WPF ownerをstealしないこと、2 WPF + BrowserのFavorite/Seenと2 WPF + Browser/thirdのRecent競合を固定する。
- malformed/lock/decode/Recycle failureで元データ保持。
- passive workflow前後でenhancement jobs hash不変。
- 5,000/20,000件でsilent truncateなし、realized UI bounded。large-catalog変更時はexact 100,000件/100 foldersでもfull extent、末尾Grid/List/Created/Modal、List末尾thumbnailを証明する。時間/working set/GCは観測値としてJSONへ残す。
- 100,000-entry Favorite/Seen fixtureはlarge actorを必ず採用し、3回すべてmodal next p95 50ms以下、Favorite action p95 65ms以下、dispatcher max gap 110ms以下にする。relative gateは各metricで`large <= max(control * 2.5, control + 10ms)`とし、数msのcontrolに対するscheduler jitterを許容しつつ実質的な退行を止める。verifier self-testはcurrent jitter caseを通し、relative regression caseと旧P1 baseline（186/251/460ms）を拒否する。small fixtureはactor非採用、large fixtureはpositive batch count、exact merge、source/Enhancement/cache isolation、close後pending 0を同時に要求する。
- screenshotは同一viewport/stateのBrowser referenceと並べて差分確認し、screenshotだけをQA完了証拠にしない。`--shot-width` / `--shot-height`はOS work areaに依存せずrequested content viewportをexactに出力する。

## 18. 現在の完成境界とCURRENT LIMITATION

Browser基準のWPF viewer workflowは実装・専用smoke済み。Bulk Favorite、Bulk Recycle、right panel resize、preview tab reload/hover/reorder/middle-close、modal end wrap/chrome/edge/swipe/feedback、Prompt tag→search、Folder bucket range selection/selected actions/collapse persistence、native Explorer FileDrop drag-outとfolder drag-inまでfocused verifier付きで実装済み。Modalはnamed focusable root、Tab/Control+Tab cycle、focused child上のEscape、close後focus return、metadata/copy/edge-zone Automationを`verify-wpf-p1b.ps1`で実動作固定している。

監査した既存journeyのP0破損は確認していないが、Browser parityと追加製品機能には本書とtruth tableのpendingが残る。100,000件/2.9〜3.2 MiBで再現していた同期shared Favorite/Seenのdispatcher停止はgeneration-aware single-writer actorで解消した。2026-07-18の47 checks / 264,857msは当時のhistorical snapshotであり、現行合格件数ではない。shared state/recent cross-runtime各20反復も別途greenで、Favorite/Seen各40 path、unknown field、last-writer policy、lock/tmp cleanupを確認した。

2026-07-20のModal filmstrip/shared Search History完成確認では、Browser focused 33/33、全unit 589 pass / 3 skip、typecheck/build、実Chrome Playwright 1/1をgreenとした。PlaywrightはSearch Historyのinput/row keyboard操作に加え、Modalのmanual-visible/900ms transient/hidden overlay Filmstrip、far navigation、Delete一段移動、console error 0を実ブラウザで確認する。WPF focused verifierはraw/normalized各32,768境界、Unicode White_Space + U+FEFF trim parity、malformed/future/Busy非破壊を通し、cross-runtime verifierは20+20 write、両worker ready-before-release、write区間668ms overlap、lock/temp residue 0を確認した。lossless全件保持を証明する`-Iterations`上限はMRU 50件内の23とする。現行aggregateは20,000件stressと24-cycle reload soak込みで55/55、462,762ms、reload 24/24がgreenである。Search HistoryのWPF keyboard証拠はhandler相当helperとcompiled XAML wiringであり、実OS routed-key eventのmanual testではない。後続`a091ec7`/`c3d4ff5`でWPF Modalへmanual/transient chrome、Filmstrip専用row/hidden overlay、上端zoom、focused-button shortcut、ViewerState保存を追加し、`verify-wpf-modal-interaction.ps1`の全項目をgreenにした。

2026-07-19のpersistent metadata index closeoutではfocused restart/corruption/cancel gateをaggregateへ追加し、現行50 checksを20,000件cold→warm stressと24-cycle reload soak込みで全greenにした。shared Favorite/Seenとshared Recentのcross-runtime各20反復も再実行し、Favorite/Seen各40 path、Recent 3 owner set、valid JSON、lock/tmp residue 0、real port/user cache非使用を再確認した。

large-catalog最終gateではexact 100,000 images / 100 foldersをcatalog・filtered・Grid ItemsSourceへ全件publishし、silent truncate 0、Grid/Listの同時realize 15/9、末尾index 99,999のGrid/List/Created/Modal到達とcanonical/visual selectionを確認した。List末尾thumbnailは165ms、flat zoom 35/50ms、Created zoom 43/60ms、anchor driftはすべて0pxだった。Viewerは4,975msで開き、metadataはbackgroundで継続して全load 33,792ms、dispatcher heartbeat最大gap 584ms、external WM_NULL最大停止522msで750ms gate内、working setは143,036,416→321,769,472 bytes、fixture cleanup成功である。FoldersはUI Automation `ExpandCollapse`によるExpanded→Collapsed→Expandedとvisual/persistence migrationをfocused gateで通している。

persistent index後の同一shape再測定はcold catalog ready 3,809ms / metadata 26,659ms / full load 30,850msでbaselineを悪化させず、同じfixture/indexを別`MainWindow`で開いたwarm phaseはcatalog ready 3,396ms / metadata 213ms / full load 3,928msだった。warmは100,000 hits / 0 misses、index read/write 65/0ms、SHA-256とmtime不変、Grid/List 15/9、末尾99,999、zoom drift 0px、warm dispatcher gap 245ms、overall WM_NULL最大停止387ms、Enhancement 0でgreen。fresh baseline比でmetadata 99.2%、full load 87.4%短縮した。

2026-07-19のcurrent-main promotionでは、WPF gallery zoom/geometry anchorを`e371b482af44e0428d9fe0d5217b236801f29cff`へ採用した。focused gateは20/600 endpoints、600=1列、旧40維持・範囲外clamp、同名別folderをcanonical pathで分離し、Sidebar/right panel/window resize/DPI/selection有無のdrift 0px、List 9/10/8 boundedを確認した。後続Modal/filmstrip layoutを含むmainでaggregate + reload soak 53/53、reload 24/24、exact 100,000 images / 100 folders、silent truncate 0、Grid/List 15/9、tail 99,999、warm hit 100,000 / miss 0、最大unresponsive 262ms / gate 750msがgreenである。

後続`5ae1e00`はFavorite/Seen writerを固定cadenceでcoalesceし、explicit drainで即時flush、同一windowのFavorite/Seen更新をkernel gateで直列化し、Favorite/Unfavorite filterが無い時の全filter/sort再構築と同一Modal画像の再decodeを避けた。`-SkipStress` aggregate 51/51とfocused latency 6/6がgreenで、large Favorite p95 0.147〜0.190ms、Modal 4.814〜5.506ms、最大dispatcher gap 29.427〜45.155msを記録した。

`a091ec7`はBrowserと同じshared `thumbnailStatusBorders` schemaを採用した。Favoriteは`#RRGGBB`で既定黄色、AI-enhancedは`rainbow | #RRGGBB`で既定虹色。Grid/Listは既存TileのFavorite/Enhanced stateへO(1) bindingし、Favorite 2px innerとEnhanced 3px outerを同時表示する。虹brushは8 stopを共有してFreezeし、thumbnail I/Oを追加しない。後続`60ed739`はdirty Favorite/Enhanced preferenceだけをlock内latest diskへmergeし、他surfaceの同時更新を保持する。focused verifierは旧hex互換、missing/reset、reload、lost update、unknown fields、malformed、busy lock、残骸0を全項目green、Release 0 warning / 0 errorで確認した。

`a1d83c8`はModalの現在表示中Original/Enhancedを容量表示とexternal openの同じresolverへ接続し、exact `0.00MB`、toggle即更新、missing/stale/invalid signature/ownership外EnhancedのOriginal fallback、canonical guard、ShellExecute failure recoveryを採用した。同じ変更でGrid/List Enterをcurrent filtered/sorted orderのModalへ接続し、後続`dbad550`と`452ac02`でnavigation後closeのcurrent primary item focus、Search/Date/Settings/Delete/Modal input/Landing隔離、OriginalSource/Keyboard focusの両方でPrompt chip等buttonのnative Enter/Spaceを固定した。`verify-wpf-external-open.ps1`、`verify-wpf-gallery-enter-modal.ps1`、Modal Enhancement/interaction/status-border verifierは全green、Release 0 warning / 0 errorである。12 owned feature blobsはcheckpoint `8ff1e52`とcurrent main `0802f95`でbyte-identical。`verify-wpf-product.ps1 -IncludeReloadSoak`は55/55、`AGGREGATE_EXIT=0`、333,899ms、reload soak 24 cycles / 40,322msで、Prompt-tag Enter isolationを含めてgreen。layout/state/stress実装・verifierは変えていないため、直前のcurrent-main exact 100,000 images / 100 folders証拠を継承する。

editable key binding focused gateは独立process 2回でwrite/hot applyとreload/resetを確認する。追加の100,000件logical selection fixtureの最終runではCtrl+Aが20ms、exact 100,000 selected、materialized visual 15件、Ctrl+Shift+A clearが13msだった。これは観測値であり、gateは各1,500ms以内、canonical count exact、visual projection 2,048件未満を要求する。同じgateがSettings/input/Delete/metadata wheel isolation、Landing中のCtrl+Plus/Ctrl+A/F/Delete/reopen無効化、nested unknownの外部delete/add mergeも固定する。

1280×820 / 1024×700の実WPF screenshotでLanding、Viewer、Settings、Folders collapsed、Unseen dots、Modal metadataを目視し、sidebar固定幅、crop/overflow、panel境界、黒backdrop、control配置を確認した。persistent metadata statusはsidebarとfooterで狭幅でも欠けず、進捗中はpolite live regionを1つだけ公開する。これはfunctional verifierの代替ではなく、現行50-check aggregateと組み合わせたvisual evidenceである。

自動化で未証明なのは、実利用環境のscreen reader、高contrast、200% text/DPIを人が連続操作するmanual evidenceであり、既知の機能欠落とは区別する。UIA `ExpandCollapse`、Modal focus cycle、Automation Name/HelpTextは自動gate済みだが、screenshotだけをaccessibility合格証拠にはしない。

CURRENT LIMITATION / P3として明示的に遅延:

- WPF単独のnative Enhancement engine、worker ownership、複数output version selector。現行の明示AI操作はlocal Browser APIへ委譲するため、Browser engineとそのactive indexが必要。
- disk cache quota/eviction。既存cache retention契約なしに自動削除を追加しない。
- 高度gesture、animation、追加visual polish。
- installer/self-contained publish、code signing、auto-update。通常のlocal `start_wpf.bat`起動とは別scope。

これらは現行P0〜P2の互換要件ではなく、別実装が勝手に補完してshared state、source、cache、配布境界を変えてはならない。Browser側Section 22のCURRENT LIMITATIONも、WPFが再現すべきMUSTではない。

「完成」を無期限に伸ばさないため、P0/P1安全契約と日常workflowを製品完成線、P2を操作効率、P3を製品判断後の拡張として分ける。残差を実装するたびにこの文書、parity ledger、focused verifierを同じcommitで更新する。
