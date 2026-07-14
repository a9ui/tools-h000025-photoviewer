# Browser Feature and Behavior Contract

最終更新: 2026-07-14

対象: H000025 PhotoViewer の Next.js ブラウザ版
基準: `origin/main` の `cf14ae4` と issue #310 の全体レビュー

## 1. この文書の使い方

この文書は、ブラウザ版に現在ある機能、操作、状態、データ契約、既知制約を、WPF など別 surface の実装基準として固定するもの。ブラウザの見た目をそのまま複製することより、ユーザーが依存している意味を維持することを優先する。

特に次の4点は回帰させない。

1. Delete 後は、同じ閲覧順の隣の画像へ継続する。
2. Favorite Lv1-5 は、複数レベルを独立に ON/OFF でき、何も選ばない状態は Favorite 全体を表す `All` になる。
3. seen は画像を明示的に選択・閲覧した時に記録し、`Unseen dots` はその状態を消さず表示だけを ON/OFF する。
4. 高画質化は明示操作でだけ開始し、通常の scan、一覧表示、preview、modal navigation では job enqueue や worker start を行わない。

## 2. Runtime と境界

- Next.js 16 / React 19 の local-first アプリ。
- 通常の production launcher は `scripts/prod_launcher.js` で、既定 URL は `http://127.0.0.1:3000/`。
- フォルダ、画像、index、favorites、settings、thumbnail、enhancement job はローカルマシン内で扱う。
- H project の Vercel deployment は、明示承認がある時だけ行う。ローカル更新と deployment は別の操作。
- ブラウザ surface と `local-native/**` は別 implementation lane。共通化するのは明文化された状態ファイルと意味契約だけ。

## 3. Landing とフォルダ選択

### 3.1 フォルダセット

- Windows の folder picker から複数フォルダを追加できる。
- picker は Windows COM を優先し、利用できない時は VBScript 経路へ fallback する。
- 絶対パスを1行1フォルダで貼り付けて追加できる。
- 大文字小文字を無視して重複フォルダを除外する。
- 選択済みフォルダを個別に外せる。
- `Ctrl+Enter` / `Cmd+Enter` で paste欄の folder path を選択セットへ追加する。scan は `Open folder set` で開始する。
- Scan ボタンの Shift-click は full verify。通常 scan は incremental fast path を使う。

### 3.2 Recent / last folder

- 最後に開いた folder set を `Open last folder set` から再利用できる。
- recent folder set は最大8件。
- ブラウザ localStorage と共有 `.cache/recent-folders.json` を使う。
- ブラウザ側の保存済み値がない時だけ shared recent を初期 import する。
- legacy PhotoViewer state は additive に import し、現行値を無条件に消さない。

### 3.3 Scan 表示

- `preparing`、`scanning`、`complete`、error の進行状態を表示する。
- 複数 root の全体 progress と、処理ファイル数、新規解析数を表示する。
- 個別フォルダの permission / transient read failure は可能な範囲で skip し、他 root の scan を続ける。
- 現状は scan cancel UI を持たない。

## 4. Index と画像配信

### 4.1 対応形式

- PNG
- JPG / JPEG
- WebP
- AVIF
- GIF

PNG では Stable Diffusion 系の `parameters` / text metadata を解析する。他形式は画像として閲覧できるが、同等の generation metadata が必ず得られるわけではない。

### 4.2 Incremental index

- root 直下と子フォルダを scan する。
- file path、mtime、size、created/birth time、PNG metadata を index cache に保持する。
- mtime と size が変わらない画像の metadata parse を再利用する。
- folder signature が変わらない古いフォルダは fast path で再利用する。
- 最近更新された scan target は conservative に再確認する。
- 削除済み file は index から除外する。
- 複数 root 間の同一 absolute path は重複表示しない。
- image URL に mtime version を付け、画像更新後の古い browser cache 混在を避ける。

### 4.3 Thumbnail / display / original

- grid/list は thumbnail cache を使う。
- right preview と modal は display cache を優先し、必要に応じて original へ fallback する。
- visible / nearby / background の優先度で thumbnail warmup を行う。
- Sharp cache はファイル100、memory 256MB の上限を持つ。
- derived thumbnail/display cache は再生成可能。source image の意味上の正本ではない。

## 5. Viewer shell

### 5.1 Header

- sidebar show/hide
- PhotoViewer ロゴから folder selection へ戻る
- active folder set の refresh。Shift-click は full verify
- tag/chip search
- `filtered / indexed` または `indexed` count
- right preview show/hide
- enhancement queue show/hide

狭幅ではロゴと count を隠し、検索と主要 icon を優先する。sidebar は gallery の幅を潰さない overlay drawer として表示する。

### 5.2 Sidebar

- active folder set summary
- folder の追加、変更、refresh
- Quick Search: Portrait (`1girl, portrait`)、Landscape、Anime、Photoreal、Clear
- Favorites only / Unrated only / Favorite Lv1-5
- Enhanced only
- Today / 7d / 30d / This year / manual date range
- folder bucket の選択、range 選択、show/hide、show selected、hide selected、invert、clear
- sort と random reshuffle
- Grid / List
- Standard / Compact / Poster
- Original / 1:1 / 2:3
- thumbnail size
- Settings
- lightweight performance counters の ON/OFF

## 6. Search と filter の意味

### 6.1 Query

- query は comma 区切り token。
- token 間は AND。全 token が一致した画像だけを表示する。
- 検索対象は現在、PNG prompt text と filename。absolute path 全体や任意 editable tag DB ではない。
- input 中の候補は index から生成した prompt tag 集計で、最大8件表示する。
- Enter / comma で chip を確定する。
- Backspace で空 input から最後の chip を外す。
- chip は drag で並べ替えられる。
- modal の prompt tag を押すと、modal を閉じて同じ search query に追加する。

### 6.2 Favorite

- 各画像の favorite 値は整数 `0..5`。`0` は unfavorite。
- `+` は最大5まで1段ずつ上げる。
- `-` は最小0まで1段ずつ下げる。0になった key は map から除去できる。
- `Favorites only` が OFF の時、level checkbox は filter に影響しない。
- `Favorites only` が ON で level を何も選ばない時は `All`。level 1-5 のどれかが付いた画像を全て表示する。
- level を選んだ時は exact-match。Lv2 は Lv2 の画像だけで、Lv2以上ではない。
- Lv1-5 は checkbox で独立選択。例: Lv1 と Lv4 を同時表示できる。
- `Unrated only` は level 0 だけを表示し、Favorites only と相互排他。
- 選択 level set は reload 後も維持する。

### 6.3 Seen / unseen

- 画像は card/list の明示 selection、preview navigation、modal navigation で seen になる。
- viewport に入っただけでは seen にしない。
- seen は additive な path map として保存する。
- `Unseen dots` は既定 OFF。
- ON の時だけ unseen card に dot/class を表示する。
- OFF にしても seen map を削除・反転しない。
- `Unseen dots` は表示設定であり、unseen-only filter や seen reset ではない。
- 現状、seen state を全消去する UI はない。

### 6.4 Date / folder / enhanced

- date range は created/birth time を使い、日付の始端から終端までを含む。
- folder bucket 非表示は server-side search に反映する。
- Enhanced only は succeeded enhancement job の source image だけを表示する。
- Favorite / Unrated / Enhanced は client-side filter。header count は server-side query/date/folder count を基準にするため、これらだけを ON にした時の count と可視 card 数が一致しない場合がある。

## 7. Sort と gallery layout

- Modified: New / Old
- Created: New / Old
- Name
- Random。seed を固定して再描画でも順序を安定させ、Reshuffle で seed を更新する。
- 日付 section は大きすぎない result set で使う。極端に大きい集合では layout cost を避ける。
- Grid は browser width と thumbnail size から列数を決める。
- List は固定 row height の virtual list。
- search result は100件 page で取得し、viewport 周辺を prefetch する。
- overscan と placeholder slot により未取得 page があっても scroll geometry を保つ。
- query、sort、filter、view mode ごとの scroll position を保存・復元する。
- Grid 上の `Ctrl/Cmd + mouse wheel`、`Ctrl/Cmd + + / - / 0` で thumbnail size を調整する。
- viewer 表示中の modified zoom input は browser page zoom に渡さず、sidebar と UI text の表示倍率を変えない。List では thumbnail size を変えず page zoom だけを抑止する。
- thumb size の範囲は40..600px。
- zoom 前後は同じ基準画像の viewport 内 offset を維持する。列数変更と日付 section header 境界を含め、見えていた場所から大きく移動させない。

## 8. Selection と preview

### 8.1 Selection

- click: 単一 selection と right preview
- Ctrl/Cmd-click: toggle multi-selection
- Shift-click: anchor から range selection
- background click: selection / preview を閉じる
- arrow keys: grid では列数単位、list では1件単位で selection 移動
- selected image は drag-out data を持つ。
- 複数 selection に Favorite +1、Favorite -1、Recycle Bin を適用できる。

設定、確認、modal、input、button、select、contenteditable が active の時は、gallery の Delete / Favorite / arrow shortcut を通さない。

### 8.2 Right preview

- single click で temporary preview を表示する。
- 画像、filename、path、dimensions、size、favorite level、prompt などを表示する。
- panel width は240..900pxで resize し、保存する。
- Open external、preview tab open、favorite 操作を持つ。
- multiple selection 時は selected count と bulk action を表示する。
- narrow viewport では panel を隠して gallery を優先する。

### 8.3 Bottom preview tabs

- double click または `Tab` action で preview tab を開く。
- active、hover thumbnail、pin/unpin、close、middle-click close、close all、last closed restore を持つ。
- `Ctrl/Cmd+Shift+T` で直前に閉じた tab を戻す。closed stack 上限は30。
- pin id は localStorage に保存する。
- 現状の制約: pin は tab close を禁止せず、page reload 後に tab image data を自動再構築しない。したがって browser の pin は完全な session restore 契約ではない。

## 9. Modal preview

- 現在の filtered/order subset を navigation source にする。
- Left / Right、画面 edge click、swipe で前後へ移動する。
- 端では wrap する。
- close 後も selection と right preview を同期する。
- wheel zoom、zoom in/out/reset、pan、horizontal flip を持つ。
- single click center で chrome hide/show、double click image で metadata sidebar hide/show。
- favorite +1 / -1 と feedback を持つ。
- open external を持つ。
- Prompt / Negative / Settings / PNG Info を表示し、copy できる。
- Prompt tag から search へ移動できる。
- enhancement start、cancel、version select、original/enhanced toggle、enhanced output delete を持つ。
- narrow viewport では metadata sidebar を隠し、toolbar を複数行にして主要操作を切らさない。

既定 shortcut:

| Action | Key |
| --- | --- |
| Next / Previous | Right / Left |
| Favorite +1 | `F` |
| Favorite -1 | `U` |
| Recycle source image | `Delete` |
| Close modal | `Escape` |
| Flip | `H` |
| Enhance | `A` |
| Zoom in / out / reset | `=` / `-` / `0` |
| Original / Enhanced | `E` 固定 |
| Metadata sidebar toggle | Space 固定 |
| Open external | Enter 固定 |

modal chrome の表示/非表示は画像中央の single click。画像の double click でも metadata sidebar を切り替える。

## 10. Delete 契約

- source image の Delete は Windows Recycle Bin へ送る。hard delete fallback はしない。
- `Confirm before delete` が ON の時は確認 dialog を出す。
- dialog の `Do not ask again` は shared settings に反映する。
- project root 内の file は削除しない。
- active index にない file は削除しない。
- unsupported extension は削除しない。
- Recycle Bin 成功後に memory index / disk index から外し、derived thumbnail/display cache を best-effort で除去する。
- modal で現在画像を削除した後は、削除前 order の同じ index に残った「次の画像」を選ぶ。
- 最後の画像を削除した時は1つ前へ移る。
- subset が空になった時だけ modal を閉じる。
- bulk delete は各 selected item を同じ guarded API で処理し、完了後 selection を clear する。
- enhancement output delete は source delete と別。managed enhance cache 内の completed output だけを削除する。

## 11. Enhancement 契約

- adapter: local Sharp test/detail、Real-ESRGAN ncnn Vulkan、ComfyUI。
- preset と scale、denoise、sharpen、detail、smoothness、brightness、contrast、saturation、PNG/JPG/WebP output を選べる。
- ncnn は requested scale 最大4、他 adapter は最大8。
- 大きい AI work/output は diagnostics を示し、明示 confirm 後にだけ開始する。
- job status: queued / running / succeeded / failed / canceled / deleted。
- queue から cancel、retry、output open、output delete を行える。
- modal から succeeded version を選択して original と比較できる。
- worker は explicit POST job creation / retry の後だけ起動する。
- browsing、scan、thumbnail、preview、modal move、filter は enqueue しない。

## 12. Settings と accessibility

- Confirm before delete
- Unseen dots
- Modal edge navigation zone 10..40%
- key binding recorder
- dialog は role/name を持つ。
- keyboard focus は accent outline で可視化する。
- viewer logo は keyboard focusable button。
- modal の文字1字 toolbar にも accessible name を付ける。

未完了:

- card/list row 自体は click-able `div` が中心で、完全な keyboard listbox/option semantics ではない。
- dialog focus trap と close 後 focus restore は未実装。
- tertiary text の token は改善したが、個別組合せの WCAG 全量計測は未完了。

## 13. 永続化と共有状態

### 13.1 Browser localStorage

| Key | Meaning |
| --- | --- |
| `pvu_last_dir_set` | 最後の folder set。改行区切り absolute path |
| `pvu_recent_dirs` | recent folder sets、最大8 |
| `pvu_server_legacy_imported` | server legacy import marker |
| `pvu_legacy_imported` | `pv_*` から `pvu_*` への browser migration marker |
| `pvu_favorites` | path -> favorite level 1..5 |
| `pvu_favorites_backup` | non-empty favorite snapshot backup |
| `pvu_view` | view mode、size、aspect、panel、sort、date、hidden folders、Unseen dots、queue visibility。旧 fixed `columns` は現行 size-driven UI に合わせて auto へ正規化 |
| `pvu_pinned_tabs` | pinned preview path IDs |
| `pvu_fav_only` / `pvu_unfav_only` | favorite / unrated filter |
| `pvu_fav_levels` | exact-match favorite level set。空配列は All |
| `pvu_fav_level` | legacy single-level fallback read only |
| `pvu_enhanced_only` | enhanced-only filter |
| `pvu_scroll_memory` | query/view/filter ごとの scroll position |
| `pvu_seen_images` | seen path map |
| `pvu_perf_enabled` | lightweight profiler visibility |
| `pvu_enhance_settings` | adapter/preset/image adjustment/output settings |

### 13.2 Shared project cache

| Path | Meaning / shared surface |
| --- | --- |
| `.cache/favorites.json` | favorite levels。browser と native が共有可能 |
| `.cache/settings.json` | confirm-before-delete と key bindings |
| `.cache/recent-folders.json` | recent/last folder set の shared contract |
| `.cache/index_<md5>.json` | root ごとの image index |
| `.cache/folders_<md5>.json` | folder signatures |
| `.cache/thumbs/**` | thumbnail cache |
| `.cache/display/**` | preview/display cache |
| `.cache/enhance/**` | managed enhancement jobs/output |

browser の seen は `pvu_seen_images` に保存し、WPF の `.cache/seen.json` とは自動同期しない。browser-only の view/filter/scroll/queue visibility も shared native state と勝手に同期しない。WPF と共有する場合は明示 migration/import contract を作る。

## 14. Local API surface

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/api/browse` | POST | Windows folder picker |
| `/api/scan` | GET/SSE | incremental/full folder-set scan |
| `/api/search` | GET | paged query/date/folder/sort results |
| `/api/tags` | GET | prompt tag suggestions |
| `/api/folders` | GET | folder buckets/counts |
| `/api/image` | GET | thumb/display/original image |
| `/api/thumbs/warm` | GET/POST | warmup status/request |
| `/api/favorites` | GET/PUT | shared favorite map |
| `/api/settings` | GET/PUT | shared behavior/key bindings |
| `/api/recent-folders` | GET/PUT | shared recent folder sets |
| `/api/legacy-state` | GET | legacy state bridge |
| `/api/open` | POST | OS default app で画像を開く |
| `/api/delete` | DELETE | guarded Windows Recycle Bin delete |
| `/api/enhance/presets` | GET | enhancement presets |
| `/api/enhance/jobs` | GET/POST | queue list / explicit enqueue |
| `/api/enhance/jobs/:id` | GET | job detail |
| `/api/enhance/jobs/:id/cancel` | POST | cancel |
| `/api/enhance/jobs/:id/retry` | POST | guarded retry |
| `/api/enhance/jobs/:id/output` | DELETE | managed output delete |
| `/api/enhance/output` | GET | managed output stream |
| `/api/enhance/isolation` | GET | enqueue/worker isolation diagnostics |

## 15. 既知制約と次の改善候補

### P1

- `pvu_view` の orphan fixed-column 値と非object snapshot は復旧するが、全fieldの型/範囲を覆う schema versioning はまだない。
- delete/open/folder/search の一部 error は console または局所 text に留まり、統一 notification surface がない。

### P2

- client-only filter の可視件数と header count が一致しない。
- preview tab pin の reload semantics が弱い。
- card/list row の keyboard semantics、dialog focus trap、focus restore が未完了。
- queue は最新8件中心で、pagination と明確な empty state を持たない。
- enhancement job polling が複数 component/context に分かれ、active job 中の rerender cost を増やし得る。
- `pvu_seen_images` と scroll memory は大きくなると browser storage stringify cost が増える。

### P3 / product decision

- scan cancel を追加するか。
- editable user tag model を導入するか。現状の tag は prompt 由来候補で、独立 tag DB ではない。
- browser pin を session restore として強化するか、単なる視覚 marker と明記するか。

## 16. Review で採用した更新

- dialog/control active 中の global Delete / Favorite / arrow shortcut を遮断。
- modifier 付き browser shortcut と、focused modal control の Enter が viewer actionへ重複して流れないようにした。
- favorite server GET が遅い時も、その間の +1 / -1 / OFF を exact current state として維持。
- query、active client filter、folder empty を区別する empty state。
- dialog role/name、toolbar accessible name、viewer logo button、focus-visible outline。
- 低 contrast token と未定義 `--text-muted` を修正。
- 390px 幅で sidebar を overlay drawer にし、header と modal toolbar の切断を緩和。
- scan/search result の到着前に gallery が空でも、結果表示後に幅を再計測して40px cardへ縮退しないようにした。
- 現行UIから操作できない旧 `pvu_view.columns` を autoへ移行し、他の表示設定は保持。
- modal toolbar の Delete も `Confirm before delete` をshortcutと同じ意味で尊重。
- sidebarの Add folder / Change folder を分離し、single/bulk delete確認面のdialog semanticsを統一。

これらは user state/cache を削除せず、Delete の Recycle Bin semantics、enhancement isolation、local-native code、deployment を変更しない。
