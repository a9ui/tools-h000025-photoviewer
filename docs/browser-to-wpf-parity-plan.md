# Browser → WPF 実装パリティ計画

最終更新: 2026-07-18

## 0. この文書の位置付け

この文書は、現行ブラウザ版の製品契約を、作りかけの WPF 版へ移すための実装判断と完成順を固定する。
見た目を機械的にコピーする計画ではない。ユーザーがブラウザ版で使っている意味、状態、操作結果を
Windows ネイティブ UI で再現し、WPF 固有の改善は契約を壊さない範囲だけ採用する。

基準:

- Browser 契約: `docs/browser-feature-contract.md`
- WPF live code: `local-native/PhotoViewer.Wpf/MainWindow.xaml`,
  `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs`, `local-native/PhotoViewer.Wpf/App.xaml`
- 初回監査 snapshot: commit `626b7dd5416f3619ae59fc66d47e79acd1a74fd5`
- 現行実装の詳細: `docs/wpf-product-spec.md`
- Section 3の「初回live状態」は差分発見時の履歴である。現在の完成/未完成判定は次のlive ledgerと`docs/wpf-product-spec.md`を優先する。

仕様の優先順位は次の通り。

1. `docs/browser-feature-contract.md` の現行契約
2. 実際の Browser UI と回帰テスト
3. WPF live code
4. この計画
5. WPF README や過去の milestone verification

WPF README や `docs/local-native/m*-verification.md` は履歴資料であり、live code と矛盾した場合は
根拠にしない。特に `docs/local-native/m*-verification.md` の多くは WinForms
`PhotoViewer.Native` の検証で、WPF の実装証拠ではない。

### 0.1 2026-07-18 live completion ledger

| Slice | 状態 | 現在の実装・証拠 |
| --- | --- | --- |
| P0A Sidebar契約 | **DONE** | Quick Search/date preset削除、Folders折り畳み、Favorite Lv1〜5 exact multi-toggle + All、Unseen dots既定OFF、F/U shortcut。`verify-wpf-p0.ps1` |
| P0B 全件catalog/zoom | **DONE** | silent 1,200 cap撤去、5,000件catalog、Grid/List bounded recycling、40〜600 zoom、viewport anchor drift 0、sidebar/text不変。`verify-wpf-p0.ps1` |
| P0C Delete | **DONE** | canonical/root/index/type再検証、Recycle Bin only、confirm/Do not ask again、next→previous neighbor、失敗時non-mutation。`verify-wpf-p0.ps1` |
| P0D 統合gate | **DONE** | 5,000件、malformed state/shared JSON、lock recovery、Favorite/Seen merge、Browser/WPF同時20回・別path各40件のcross-runtime stress、zoom/Delete、passive enhancement isolation。`verify-wpf-p0.ps1`, `verify-cross-runtime-shared-state.ps1` |
| P1 Search/date/folder | **DONE** | comma AND、filename+PNG prompt、Created/Birth date、Add/Change folder分離、全件background search + 150ms debounce + stale cancellation。`verify-wpf-p1a.ps1`, `verify-wpf-search-stall.ps1` |
| P1 State/error/a11y | **DONE** | versioned/normalized unknown-field-preserving state、debounced atomic write、recoverable inline status、focus return、shortcut guards、AutomationName。`verify-wpf-p1b.ps1` |
| Formats | **DONE** | PNG/JPEG/WebP/GIF/AVIF/BMP/TIFF fixture decode/fallback。`verify-wpf-formats.ps1` |
| Right preview | **DONE** | 240〜900 resize/persist、single/multi state、latest-selection guard。`verify-wpf-right-panel.ps1` |
| Bulk Favorite | **DONE** | exact Lv0〜5、all +1/-1、単一atomic merge transaction、disk-latest preservation、rollback/no-op reassert。`verify-wpf-bulk-favorite.ps1` |
| Preview tabs | **DONE** | open/activate/pin/hover/close/reopen/close-all、最大30、drag/Alt+Shift reorder、middle-close、order/active reload restore、filter外tab/pinのfull-catalog復元、focus/Automation。`verify-wpf-preview-tabs.ps1`, `verify-wpf-preview-tab-reorder.ps1`, `verify-wpf-rapid-ui-state.ps1` |
| Preview hover performance | **DONE** | cancellable background decode、stale result discard、corrupt input inline recovery、dispatcher heartbeat。`verify-wpf-preview-tab-hover.ps1` |
| Modal end wrap | **DONE** | filtered orderで先頭↔末尾を循環。`verify-wpf-modal-wrap.ps1` |
| P2 Modal interaction | **DONE** | delayed single-click chrome、accessible edge zones、zoom-safe swipe、transient feedback、double-click metadata guard。`verify-wpf-modal-interaction.ps1` |
| P2 Bulk Recycle | **DONE** | count confirmation、single guard再利用、Recycle-only per-source attempt、成功分だけcleanup、部分失敗保持、filtered neighbor/empty。`verify-wpf-bulk-recycle.ps1` |
| P2 Folder sidebar | **DONE** | Ctrl/Shift range selection、Show/Hide selected、primary selection、collapse/selection reload、v1→v2 migration。`verify-wpf-folder-buckets.ps1` |
| P2 Prompt tag→search | **DONE** | stable dedupe chip、click/Enter/Space、query append/persist、modal close、Search focus、Automation/fallback/isolation。`verify-wpf-prompt-tag-search.ps1` |
| P2 Explorer FileDrop | **DONE** | Grid/List/right preview、system threshold、selected display order、canonical/root/catalog/type/existence guard、Copy payload、Automation/isolation。`verify-wpf-file-drag-out.ps1` |
| P1 Explorer folder drag-in | **DONE** | Landing/Viewer folder FileDrop、canonical existing directory、case-insensitive dedupe、append/rescan、reference-only status/Automation/isolation。`verify-wpf-folder-drag-in.ps1` |
| P2 remaining | **DONE** | 現行ledger上の実装残なし。統合/visual/stressで再現した欠陥だけを修正する。 |
| P3 Enhancement ownership | **DEFER** | WPFは成功済みoutputのread-only表示のみ。enqueue/workerは製品判断まで実装しない。 |

## 1. 判定記号

| 判定 | 意味 | 実装時の扱い |
| --- | --- | --- |
| `ADOPT` | 現在の WPF 実装が Browser の意味と一致する | 回帰テストで保護し、不要な作り直しをしない |
| `ADAPT` | 機能はあるが意味、既定値、操作、保存方法が違う | 既存コードを Browser 契約へ変更する |
| `ADD` | Browser 契約に必要だが WPF にない | 新規実装する |
| `NATIVE-EXTENSION` | Browser にはないが Windows ネイティブでは有用 | Browser 契約を変えず、既定 UI を圧迫しない場合だけ残す |
| `DEFER` | 完成条件から意図的に後ろへ送る | P0～P2 の途中で先回り実装しない |
| `DROP` | Browser から削除済み、または誤った旧仕様 | WPF からも削除し、state/smoke の旧期待値も除去する |

優先度:

- `P0`: WPF を Browser 仕様の製品として成立させる必須条件。
- `P1`: 日常利用の正確性、状態保存、検索、失敗時の安全性。
- `P2`: 操作効率、複数選択、preview/modal の完成度。
- `P3`: 明示的に後回しにできる高度機能、磨き込み、製品判断待ち。

## 2. 絶対に変えない製品契約

1. Favorite 値は共有 path ごとの `0..5`。
2. Favorite filter は Lv1～Lv5 の独立 ON/OFF、各レベル完全一致。何も選ばない状態が `All`。
3. `Unseen dots` は seen データを変えない表示設定で、既定値は OFF。
4. seen は明示選択、preview 表示、modal navigation で付ける。画面内に realize しただけでは付けない。
5. source Delete は Windows Recycle Bin のみ。成功後は現在の filtered order の隣へ継続し、
   hard-delete fallback を持たない。
6. 通常の閲覧、preview、modal navigation から enhancement job や worker を開始しない。
7. 画像件数を無言で切り捨てない。全件 index と UI realization 上限を別概念として扱う。
8. `.cache/favorites.json` と `.cache/recent-folders.json` は Browser と共有する。
9. Browser/WPF seen は `.cache/seen.json` の加算型true-marker mapを共有し、Browser localStorageはlocal mirrorとしてunionする。削除・false化・全置換は自動同期しない。
10. 既存ユーザー state/cache を削除して移行しない。
11. Browser production serverはloopback-onlyとし、WPFはHTTP server自体を必要としない。

Browser 側の根拠:

- Favorite: `BR-DATA-005`, `BR-FAV-001`～`003`
- seen / dots: `BR-DATA-006`, `BR-SEEN-001`～`002`
- zoom / virtualization: `BR-GAL-001`, `BR-GAL-006`～`007`
- selection: `BR-GAL-008`～`010`
- Delete: `BR-DEL-001`～`007`
- enhancement isolation: `BR-ENH-001`～`007`
- Settings / state: `BR-SET-001`～`004`, `BR-PER-001`～`005`

## 3. Browser → WPF 初回監査差分表（履歴）

このSectionは`626b7dd`時点で「何を直す必要があったか」を残す履歴であり、現在状態ではない。現在の実装済み/未実装はSection 0.1と`docs/wpf-product-spec.md`を使う。行内の古いline numberや「なし」という記録をlive判定へ使わない。

### 3.1 起動、folder set、scan、format

| 項目 | 判定 | 優先度 | WPF live 状態と証拠 | 完成条件 |
| --- | --- | --- | --- | --- |
| ネイティブ runtime | `ADOPT` | — | .NET 8 WPF。Node/server/WebView 不要。 `PhotoViewer.Wpf.csproj:5-8` | ローカル folder の閲覧に server を要求しない |
| 複数 folder picker / paste / recent / last | `ADOPT` | P1 | landing と共有 recent 読み込みがある。 `MainWindow.xaml:510-581`, `MainWindow.xaml.cs:192-206,3914-3965,4512-4669` | 複数 root、paste、remove、last、recent が reload 後も再現する |
| Add folder と Change folder | `ADAPT` | P1 | 両方が同じ `OpenFolder_Click`。 `MainWindow.xaml:99-104`, `MainWindow.xaml.cs:190-206` | Add は現在 set へ追加、Change は現在 set を保持した folder-set 編集画面へ戻す |
| 再帰 scan / 重複排除 / permission skip | `ADOPT` | P0 | recursive enumeration と path dedupe がある。 `MainWindow.xaml.cs:250-258,643-680` | 一部 folder の失敗で全 scan を失わず、重複画像を二重登録しない |
| 1,200 枚上限 | `ADAPT` | P0 | `MaxLoadedImages = 1200` と `Take(MaxLoadedImages)`。 `MainWindow.xaml.cs:30,250-258` | silent cap を撤去し、1,201 枚目以降も検索、preview、modal 対象になる |
| Grid bounded realization | `ADOPT` | P0 | 96 件単位、最大 384 件の realized window。 `MainWindow.xaml.cs:36-38,3110-3200` | 全件 index を維持したまま realized item 数が bounded |
| List virtualization | `ADD` | P0 | `StackPanel` に full collection を bind。 `MainWindow.xaml:307-330` | 5,000 件で container 数が viewport 近傍に限定される |
| 対応形式 | `ADAPT` | P1 | PNG/JPEG/WebP/BMP/GIF/TIFF。AVIF なし。 `MainWindow.xaml.cs:24-27,1729-1766` | PNG/JPEG/WebP/GIF/AVIF を fixture で decode。BMP/TIFF は extension として別判定 |
| BMP / TIFF | `NATIVE-EXTENSION` | P2 | WPF decoder 対象として宣言。 `MainWindow.xaml.cs:24-27` | format fixture が通る場合だけ正式対応として残す |

### 3.2 Sidebar、検索、filter、sort

| 項目 | 判定 | 優先度 | WPF live 状態と証拠 | 完成条件 |
| --- | --- | --- | --- | --- |
| Quick Search | `DROP` | P0 | 旧 preset UI と click handler が残る。 `MainWindow.xaml:108-125`, `MainWindow.xaml.cs:3026-3034` | UI、handler、state、旧 smoke 期待値を削除 |
| 検索 grammar | `ADAPT` | P1 | 空白区切り AND。path/group/size/date 等も対象。 `MainWindow.xaml.cs:3215-3236` | カンマ区切り AND。空白 trim、空 token 無視、順不同で同じ結果 |
| PNG prompt 検索 | `ADAPT` | P1 | 初期 `Prompt=file.FullName`。実 prompt は preview 時の lazy metadata で Tile へ戻らない。 `MainWindow.xaml.cs:683-715,2130-2207` | filename と index 済み PNG prompt を、preview 前から検索できる |
| suggestions / chips / prompt tag action | `ADD` | P2 | plain TextBox のみ。 `MainWindow.xaml:44-52` | suggestions、chip add/remove、prompt tag→search を実装。P1 grammar 完了後に着手 |
| Favorite 値 0..5 / +1 / -1 | `ADOPT` | P0 | shared favorites 読み書きと step 操作あり。 `MainWindow.xaml.cs:823-864,981-999,2726-2761` | 既存値を変換せず保持し、0..5 以外は境界で正規化 |
| Favorite filter | `ADAPT` | P0 | 単一 RadioButton の `Lv N+` threshold。 `MainWindow.xaml:131-145`, `MainWindow.xaml.cs:2601-2698,3241-3248` | Lv1～5 独立 checkbox、exact-match、空集合=`All`、複数選択保存 |
| Favorite shortcut | `ADAPT` | P0 | `F` が 0↔5、減算は `X`。 `MainWindow.xaml.cs:4682-4765` | `F` は +1、`U` は -1。TextBox/control/modal guard を共通化 |
| Unrated | `ADAPT` | P1 | 専用 checkbox と Favorite 排他制御。 `MainWindow.xaml:131-145`, `MainWindow.xaml.cs:2525-2537` | level 0 exact として定義し、Lv checkbox/All との状態遷移を一意にする |
| seen store / 明示選択 | `ADOPT` | P0 | WPF `.cache/seen.json`、legacy merge、選択時保存。 `MainWindow.xaml.cs:1007-1129,1898-1980` | realize/scroll だけでは増えず、明示表示時だけ additive に増える |
| Unseen only | `NATIVE-EXTENSION` | P1 | filter が実装済み。 `MainWindow.xaml:143-145`, `MainWindow.xaml.cs:3064-3270` | dots 設定から独立し、ON/OFF で seen JSON を変更しない |
| Unseen dots | `ADAPT` + `ADD` | P0 | Grid は常時 dot、List は dot なし、設定なし。 `App.xaml:319-430`, `MainWindow.xaml:278-330` | Settings に既定 OFF の toggle。Grid/List 両方へ同一可視性を適用 |
| date preset | `DROP` | P0 | Today/7d/30d/This year/Clear が残る。 `MainWindow.xaml:146-170`, `MainWindow.xaml.cs:3591-3764` | preset UI、handler、state、旧 smoke 期待値を削除。manual From/To は残す |
| manual date semantics | `ADAPT` | P1 | `ModifiedUtc` で絞り込む。 `MainWindow.xaml.cs:3255-3270` | Browser と同じ Created/Birth date。境界日は local date で固定 fixture 化 |
| Folders collapse | `ADD` | P0 | 静的見出しで常時展開。 `MainWindow.xaml:174-204` | 既定展開。見出し button で折り畳み、child focus を残さない |
| Folder bucket 基本操作 | `ADOPT` | P1 | Show All / Hide All / Invert と bucket 単体 toggle。 `MainWindow.xaml:174-204`, `MainWindow.xaml.cs:2539-2599` | hidden state を root 間で安定した key に保存 |
| Folder bucket 範囲・選択操作 | `ADD` | P2 | bucket の複数/範囲選択と Show/Hide selected がない | Ctrl/Shift 選択、Show selected、Hide selected、Invert を一貫させる |
| Sort | `ADOPT` | P1 | Modified/Created/Name/Random/Reshuffle 実装済み。 `MainWindow.xaml:206-220`, `MainWindow.xaml.cs:3273-3300,3521-3588` | reload 後も mode/方向/seed が再現し、modal order も同じ |
| date section | `ADAPT` | P2 | Modified date で常時 group。 `MainWindow.xaml.cs:717-724` | Browser の sort と bounded-result 条件へ合わせ、非該当時は表示しない |

### 3.3 Gallery、選択、zoom

| 項目 | 判定 | 優先度 | WPF live 状態と証拠 | 完成条件 |
| --- | --- | --- | --- | --- |
| Grid / List | `ADOPT` | P0 | 二つの view と選択同期がある。 `MainWindow.xaml:278-330`, `MainWindow.xaml.cs:1898-1980` | view 切替で primary/selected set/scroll context を失わない |
| Standard / Compact / Poster | `ADOPT` | P1 | 3 style 実装済み。 `MainWindow.xaml:224-269` | style ごとの card size と state reload を screenshot で固定 |
| Original / 1:1 / 2:3 | `ADOPT` | P1 | Original/Square/Portrait。 `MainWindow.xaml:224-269`, `MainWindow.xaml.cs:3456-3588` | crop/fit の意味を Browser と一致させ、state を保持 |
| Zoom 範囲 | `ADAPT` | P0 | 130..280。 `MainWindow.xaml:224-269` | 40..600、Ctrl+wheel、+/-/0 が同じ clamp と reset 値を使う |
| Zoom viewport anchor | `ADD` | P0 | slider/card size のみ更新し scroll 補正なし。 `MainWindow.xaml.cs:3381-3409` | zoom 前の viewport 中央画像と相対位置を zoom 後も維持 |
| Zoom と sidebar | `ADOPT` を検証 | P0 | WPF card slider は Browser browser-zoom とは別だが、回帰証拠がない | card zoom で sidebar/header/font/window scale が変わらない smoke を追加 |
| Zoom と List thumbnail | `ADAPT` | P0 | List thumbnail も card size に追随。 `MainWindow.xaml.cs:3417-3435` | Grid zoom は List row/thumb の寸法を変えない |
| Ctrl/Shift 複数選択 | `ADOPT` | P1 | Extended selection、range/toggle、Grid/List 同期。 `MainWindow.xaml.cs:1898-1980` | range、toggle、background clear、primary の fixture を維持 |
| Bulk Favorite | `ADD` | P2 | 複数選択件数はあるが一括 favorite UI なし | 全選択へ同一 level 設定。部分失敗を隠さず共有 JSON を一回でcommit |
| Drag out | `NATIVE-EXTENSION` | P2 | Grid/List/right previewからguarded FileDrop Copy。`verify-wpf-file-drag-out.ps1` | system threshold、selection order、canonical/root/catalog/type/existence guard、state/data isolationを維持 |
| Show in folder | `NATIVE-EXTENSION` | P2 | Right Preview / Modalからguarded Explorer select。unicode/space/apostrophe path、exact ArgumentList、focus/Automation、generic failure、state/source isolationを`verify-wpf-explorer-reveal.ps1`で固定 | Browser Openの意味は変更しない。 |
| Folder drag-in | `NATIVE-EXTENSION` | P1 | Landing/Viewer Explorer FileDrop accepts only canonical existing folders, case-insensitive dedupes and appends by reference. `verify-wpf-folder-drag-in.ps1` | no file copy/move, landing draft update, viewer rescan, rejection/status/a11y and temp-store isolation |

### 3.4 Right preview、tabs、modal、Delete

| 項目 | 判定 | 優先度 | WPF live 状態と証拠 | 完成条件 |
| --- | --- | --- | --- | --- |
| Right preview 基本 | `ADOPT` | P1 | image/favorite/open/tab/metadata。 `MainWindow.xaml:335-439` | latest-selection guard を維持し、古い decode が新選択を上書きしない |
| Right panel resize / persist | `ADD` | P2 | 列幅 340 固定、GridSplitter なし。 `MainWindow.xaml:82,335-439` | 240..900 の範囲で resize、reload 後に幅を復元 |
| Right preview bulk state | `ADD` | P2 | single primary のみ | 複数選択時に件数、bulk favorite、bulk recycle を表示 |
| Preview tabs 基本 | `ADOPT` | P1 | open/hover/activate/pin/close/reopen/close-all。 `MainWindow.xaml:442-489`, `MainWindow.xaml.cs:2807-3034` | 各操作と close stack 上限を回帰テストで保護 |
| Open tab 集合の reload | `ADAPT` | P2 | pinned path ID は保存するが open tabs は自動復元しない。 `MainWindow.xaml.cs:2874-2919,4481-4497` | 復元する仕様なら tab order/active/pin をまとめて保存。採用しないなら文言を明確化 |
| Modal 基本 | `ADOPT` | P1 | filtered order、zoom/pan/flip、metadata、Original/Enhanced。 `MainWindow.xaml:620-747`, `MainWindow.xaml.cs:3983-4385` | ordinary navigation で enhancement job を作らない |
| Modal end wrap | `ADAPT` | P2 | 端で clamp。 `MainWindow.xaml.cs:4336-4356` | next/previous が末尾/先頭で wrap |
| Modal chrome / edge / swipe | `ADAPT` | P2 | button/key navigation 中心。画像 single-click chrome toggle なし。 `MainWindow.xaml.cs:4141-4298` | native equivalent を明文化し、edge zone、chrome、操作 feedback を実装 |
| Modal metadata tag→search | `ADD` | P2 | Prompt/Negative/Settings/copy はあるが tag action なし。 `MainWindow.xaml:697-737`, `MainWindow.xaml.cs:2130-2207` | prompt tag 操作で modal を閉じ、検索条件へ重複なく反映 |
| Source Delete | `ADD` | P0 | Delete button disabled、処理なし。 `MainWindow.xaml:386-388` | Windows Recycle Bin の単体削除、確認、neighbor 継続、empty close |
| Delete safety guard | `ADD` | P0 | なし | active root 内、index 登録済み、対応 image type、canonical path を全て確認 |
| Bulk Delete | `ADD` | P2 | なし | 単体 Delete 契約が安定した後、選択 snapshot を一括 Recycle Bin へ送る |

### 3.5 Settings、state、error、accessibility、enhancement

WPF App SettingsにはAbout / Diagnosticsを追加する。clipboard exportはsafe build/runtime/counts/safety fieldsだけで、private stateや画像情報をexportしない。clipboard拒否はinline live statusで知らせる。

| 項目 | 判定 | 優先度 | WPF live 状態と証拠 | 完成条件 |
| --- | --- | --- | --- | --- |
| App Settings surface | `ADD` | P0 | app settings なし。modal の Settings は PNG generation metadata。 `MainWindow.xaml:697-737` | Confirm before delete、Unseen dots、必要な modal 設定を一か所へ配置 |
| Runtime / Version | `ADAPT` | P1 | native build identityを安全に表示・copyするsurfaceなし | Product、assembly/source revision、dirty/clean、build ID/time、architectureをread-only表示。nativeに存在しないserver portは表示せず、path/state/cache/process IDをcopyしない |
| WPF state schema | `ADAPT` | P1 | version なし JSON。scalar favorite threshold 等を保存。 `MainWindow.xaml.cs:4387-4508,5318-5337` | schema version、field normalization、未知 field 保持、旧 state の additive migration |
| State write | `ADAPT` | P1 | UI thread で同期 `File.WriteAllText`。 `MainWindow.xaml.cs:4470-4500` | debounce、atomic replace、失敗時に既存ファイル保持、UI thread を止めない |
| 保存対象 | `ADAPT` | P1 | mode/sidebar/right/scroll/dots/open tabs 等が不足。 `MainWindow.xaml.cs:4481-4497` | Browser 契約と native 必要項目を明示し、reload fixture で全項目確認 |
| Error surface | `ADD` | P1 | status、MessageBox、silent catch が混在。例: decode failure は null。 `MainWindow.xaml.cs:1729-1766` | recoverable error を統一表示し、source/shared state を失わない |
| Accessibility | `ADD` | P1 | logo が mouse-only TextBlock。focus/AutomationName 証拠不足。 `MainWindow.xaml:32-41` | keyboard 到達、focus return、読み上げ名、disabled reason、dialog focus trap |
| Enhancement read-only view | `ADOPT` | P1 | 成功済み job/output のみ読む。 `MainWindow.xaml.cs:898-964` | missing output/job malformed でも source browsing を継続 |
| Enhancement enqueue/worker | `DEFER` | P3 | WPF は enqueue しない | ownership と job schema の製品判断後だけ実装 |
| Enhanced output delete/version UI | `DEFER` | P3 | なし | source Delete と別権限・別rootで設計後に実装 |

## 4. 段階的完成順

### WPF-P0A — Sidebar契約と状態意味の復旧

実装:

1. `DROP`: Quick Search を UI、handler、旧 smoke から削除。
2. `DROP`: Today / 7d / 30d / This year / preset Clear を削除。manual From/To は残す。
3. `ADD`: Folders を既定展開、見出し button で折り畳み可能にする。
4. `ADAPT`: Favorite filter を独立 Lv1～5 checkbox、exact-match、empty=`All` に変更。
5. `ADAPT`: scalar `FavoriteFilterLevel` を level set へ additive migration。
6. `ADD`: `Unseen dots` setting を既定 OFF で追加し、Grid/List 両方へ適用。
7. `ADAPT`: `F` +1、`U` -1 と共通 shortcut guard。

受入条件:

- fixture は favorite 0,1,2,3,4,5 を各1枚持つ。
- All は level 1～5 の5枚、Lv1 は1枚、Lv1+Lv4 は2枚。全 checkbox OFF で5枚へ戻る。
- reload 後も Lv1+Lv4 を保持。旧 scalar state は対応する単一 level set へ一度だけ移行。
- dots 初回起動は Grid/List とも0個。ONで unseen 件数と同数、OFFへ戻しても seen JSON は同一。
- `Unseen only` toggle は dots visibility と独立。
- Quick Search と date preset の可視 text、focus target、event handler が0件。
- Folders は初回展開。collapse 中は child が非表示かつTab移動対象外。

### WPF-P0B — 全件catalog、List virtualization、zoom anchor

実装:

1. `Take(1200)` と「1200+ loaded」の曖昧表示を撤去。
2. 全件の軽量 catalog/query order と、thumbnail/visual realization を分離。
3. Grid の bounded realization を保護し、List を virtualizing panel へ変更。
4. search/filter/sort/modal/neighbor は常に全件 catalog を対象にする。
5. zoom を 40..600 に統一し、Grid viewport 中央画像を anchor として補正。
6. Grid zoom が sidebar/header/font/List row sizeを変えないよう分離。

受入条件:

- 5,000枚 fixture の total/indexed が5,000。
- 1,201枚目以降をfilename検索し、選択、preview、modal表示できる。
- Grid/List の realized container 数が viewport と overscan に対して bounded。
- 先頭、中間、末尾へ往復して欠落、重複、order drift がない。
- zoom 100→300→80 後も基準画像が viewport 内に残り、中心との差が許容範囲内。
- sidebar/header の実測幅とfont sizeが zoom 前後で同一。
- cold scan、warm load、selection-to-preview、peak memory を変更前後で記録。

### WPF-P0C — Settings基盤と安全なDelete

実装:

1. App Settings に Confirm before delete と Do not ask again を追加。
2. 単体 source Delete を Windows Recycle Bin のみで実装。
3. canonical path、active root、current index、対応拡張子を削除直前に再検証。
4. 成功後に現在の filtered order の次、なければ前へ継続。
5. 最後の1枚なら modal/preview を閉じ empty state へ移る。
6. Recycle Bin 失敗時は catalog/stateから消さず、hard deleteへfallbackしない。
7. 成功時はselection/anchor、modal order、right preview cache、open/active preview、pin、closed-tab history、reveal、persisted tab/pinから削除pathをpurgeし、Pin/Restore/reloadで復活させない。
8. Favorite/Seenはmulti-owner/path履歴として保持し、Enhancement job/outputは別削除権限として保持する。Source Deleteからfilesystem scan、output delete、job deleteを開始しない。
9. bulk delete は単体契約の回帰が安定するまで P2。実装時はsuccessだけ選択解除し、failureを選択したままretry可能にする。

受入条件:

- Delete smoke は専用 temp folder に複製した fixture だけを使う。
- cancel では source、catalog、selection、favorite、seen が不変。
- 3枚のmiddle削除→next、last削除→previous、only削除→emptyを確認。
- filter subset 内でも同じ neighbor rule。
- active root外、index外、unsupported type、canonicalize失敗を拒否。
- Recycle Bin API failureでsourceとcatalogを保持し、利用者へ復旧可能なerrorを表示。
- favorite/seen/recent/enhancement job fileの他エントリーを破損しない。
- Recycle成功後の揮発UI/persisted tab/pinにsource pathがなく、Favorite/Seen/Enhancement履歴には同じpathが残ることを同じtestで確認。

### WPF-P0D — P0統合gate

- P0A～P0Cのsmokeを同一commitで実行。
- 5,000枚fixture上でfavorite/dots/folder collapse/zoom/Deleteを連続操作。
- reload後もstateが復元し、既存state/cacheを削除していない。
- ordinary browsing前後でenhancement job fileとworker状態が不変。
- malformed shared JSON、同時更新、Recycle Bin failureを含むfailure testを通す。

### WPF-P1 — Search、date、state、error、accessibility

実装:

- comma AND、filename + indexed PNG prompt。
- Created/Birth dateによるmanual From/To。
- versioned/normalized WPF state とdebounced atomic write。
- Add folder / Change folder の意味分離。Change はcurrent setを保持した編集画面へ戻す。
- AVIF decoder採否とformat fixture。
- unified error surface。
- focus return、tab order、AutomationName、shortcut guard。
- read-only Runtime / Versionとsafe Copy diagnostics。nullable/invalid build metadataでもSettings本体を塞がない。

受入条件:

- prompt-only、filename-only、2-token AND、comma whitespace、no-resultをfixtureで固定。
- createdとmodifiedが異なるfixtureでCreated date意味を証明。
- malformed/null/out-of-range旧stateでstartup crashせず、正常fieldを可能な範囲で保持。
- rapid search/resize/filterでUI thread stallとstate破損がない。
- permission/decode/open failure後も他画像を閲覧できる。
- Runtime copyにproject/user/state/cache pathがなく、長いbuild identityがsmall windowで横overflowしない。

### WPF-P2 — 操作完成

- Folder bucket Ctrl/Shift選択、Show selected、Hide selected。
- Bulk Favorite、単体契約を再利用したBulk Recycle Bin。
- right panel resize/persist、bulk state。
- modal wrap、edge-zone、chrome、操作feedback。
- Preview tab のmiddle-click/reorder/reload契約。
- suggestions/chips、prompt tag→search（実装済み。live ledgerとfocused verifierを正とする）。
- date sectionの条件整合。

各sliceは一つのworkflowとして閉じ、P0の5,000枚、state、Delete、enhancement isolationを毎回再実行する。

### WPF-P3 — 製品判断後

- Enhancement enqueue/queue/cancel/retry/output delete/version selector。
- 高度なgestureとvisual polish。

Enhancementを実装する場合も、最低条件は次の通り。

- passive browsingでenqueue 0、worker start 0。
- explicit action 1回でjob 1件。
- output deleteとsource Deleteを別権限・別rootで扱う。
- managed output root外を削除しない。

## 5. 実装リスクと抑止策

### R1. 巨大 code-behind

現状の `MainWindow.xaml.cs` は約5,600行で、scan、query、selection、preview、state、
shared JSON、modal、shortcut が一つに集中している。P0機能を直接追加し続けると、
Favorite変更がselectionやmodal orderを壊すような回帰が起こりやすい。

抑止策:

1. 大規模全面rewriteはしない。先に現行smokeをcharacterization testとして固定。
2. P0で必要な境界だけを順番に抽出する。
   - `ImageCatalog`: 全件とquery order
   - `GalleryViewport`: realized rangeとanchor
   - `ViewerStateStore`: version/migration/atomic save
   - `SharedJsonCoordinator`: favorite/recentのread-merge-write
   - `RecycleBinDeleteService`: guardと結果
3. UI event handlerはstate遷移を呼ぶだけにし、filter/delete/persistence規則を直接持たせない。
4. 抽出ごとに挙動を変えないcommitと契約変更commitを分ける。

### R2. 大量画像

`Take(1200)` を消すだけでは、非virtualized List、thumbnail decode、検索毎の全件filterによって
UI freezeやmemory急増が起きる。全件を「保持すること」と全件を「visual/decodeすること」を分離する。

抑止策:

- catalog recordはpath、日時、favorite、seen、軽量metadataに限定。
- thumbnailはcancel可能な遅延decodeとbounded cache。
- query結果は全件を表すが、WPF containerはviewport+overscanのみ。
- search TextChangedはdebounceし、古いquery/decode結果が新stateを上書きしないgeneration guardを持つ。
- 5,000枚を常用gate、20,000枚をstress/性能観測gateとする。20,000 exact、Grid/List bounded、tail search/modal、stale cancellation/heartbeatを固定し、silent truncateはどの件数でも禁止。

### R3. 共有 JSON の競合

BrowserとWPFは `.cache/favorites.json` と `.cache/recent-folders.json` を共有する。

Recentは最大12個のdistinct folder setを保持する。共有書込みは両runtimeでcreate-new `<target>.lock` とread/merge/atomic replaceを使い、`lastFolderSet` は単一スロットなので最後に成功したwriterを採用する。各writerの最新setは`recentFolderSets`側で保持する。
現在の同期 `File.WriteAllText` は、同時更新、途中終了、読み取り中の上書きでlast-writer-winsや破損を
起こし得る。seenはWPF `.cache/seen.json` とBrowser localStorageで所有者が異なるため、
同じ仕組みで自動同期してはいけない。

抑止策:

1. 書き込み直前に最新disk stateを再読込し、変更対象keyだけmerge。
2. temp fileへflush後、同一volumeでatomic replace。
3. 短いprocess間lock、bounded retry、mtime/hash競合検知。
4. malformed fileを空として上書きしない。元ファイルを保持し、recoverable errorとして止める。
5. 未知fieldと他path entryを保持。
6. Bulk Favoriteは各画像ごとの書き込みではなく、一つのmerge transactionとしてcommit。
7. WPF/Browser seenはshared `.cache/seen.json`へadditive unionする。false/delete/resetは自動同期しない。

### R4. Delete

Deleteは唯一の破壊的workflowで、selectionやfiltered orderの不整合が実ファイル損失へ直結する。

抑止策:

- testは必ずtemp copy。
- delete直前にcanonical pathとactive root membershipを再評価。
- shell Recycle Bin成功を確認してからcatalogを更新。
- hard delete APIをfallbackとして持たない。
- Source Recycle成功後はWPF-owned揮発UI/persisted tab/pinだけをpurgeする。Favorite/Seen shared JSONとEnhancement job/outputはmulti-owner/history/別削除権限なのでcascade cleanupしない。
- neighborは削除前のcurrent filtered orderとprimary indexから決定し、未選択画像へ飛ばない。

### R5. State migration

現在はunversioned stateにscalar favorite thresholdを保存している。新しいlevel setやdots設定を
単純に追加すると、旧stateで起動不能、または既定値誤適用が起きる。

抑止策:

- version 0 readerを残し、scalar thresholdは同じ数字のsingle exact levelへ移行。
- dots fieldなしは必ずOFF。
- date presetは移行せずmanual rangeだけ保持。
- 未知fieldを理由に全stateを捨てない。
- migration結果を再保存する前に正常化し、元stateのbackupまたはatomic replaceを使う。

## 6. 各milestoneの共通gate

1. Debug / Release build 0 error。
2. 対象workflowの専用smokeと既存startup/state/favorites/seen/recent/folder-set/preview/modal smoke。
3. 5,000枚fixtureで件数、query order、scroll、selection、previewを確認。
4. Deleteはtemp copy以外へ実行しない。
5. ordinary browsing前後でenhancement job fileとworkerが不変。
6. screenshotでsidebar幅、text fit、focus、overlap、Grid/List、modalを確認。
7. malformed/permission/decoder/shared-state conflictを含むfailure test。
8. user/shared stateを削除しない。migrationはadditive、atomic、または明示import/exportのみ。
9. 実行commit、コマンド、結果、fixture、screenshot、console/errorをWPF専用verificationへ記録。

## 7. 完成の判定

WPF版を「Browser仕様を基に完成」と呼べるのは、少なくともP0とP1が完了し、次を満たした時。

- Favorite Lv1～5＋All、Unseen dots既定OFF、Folders collapseが仕様通り。
- Quick Searchとdate presetが存在しない。
- 1,200枚を超えても全件が検索・preview・modal・Delete対象。
- zoomでsidebarが変化せず、見ていた画像をviewport内に維持。
- DeleteがRecycle Bin、guard、confirm、neighbor、failure保持を満たす。
- search/date/state/shared JSONの意味がfixtureで固定されている。
- passive browsingでenhancement副作用がない。
- 既存ユーザーstate/cacheを消さずに移行できる。

P2は「日常操作の完成」、P3は「製品判断後の拡張」として分ける。P3が未実装であることだけを理由に、
Browser基準のWPF完成を無期限に延ばさない。
