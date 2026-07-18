# WPF large-catalog and Browser-parity recovery

## Objective

Browser版を意味の正本として、WPF版が約100,000枚・複数folderの実利用でも、全件catalogと末尾までのscroll geometryを先に成立させ、可視範囲だけを軽量に描画・thumbnail decodeする状態へ直す。同時に、日付section、Modal操作、共有Favorite/Seen、gallery aspect、明示AI enhancement導線をBrowser契約へ戻す。

## User-visible failures in scope

- 複数folder・約100,000枚で初期表示が遅い、または読み込めないように見える。
- Gridが全画像を持たず、下へ進むたびに小さなwindowを追加するため末尾到達が現実的でない。
- thumbnailが可視範囲より全体処理に引っ張られ、表示が遅い。
- Created以外のsortでも日付sectionが出る。
- Modalのボタン、左右edge navigation、黒いbackdrop closeがBrowserの意味と一致しない。
- linked worktree launcherがBrowserとは別の`.cache`を読み、Favorite/Seen/Enhancement履歴が消えたように見える。
- `Original / 1:1 / 2:3`のcard geometryとcrop契約が一致しない。
- WPFに明示AI enhancementの操作面がない。

## Required contract

1. `ItemsSource`はfiltered/sorted全件を保持し、scroll extentは最初から全件分を持つ。silent capと「末尾へ行くためのload more」は禁止する。
2. UI containerとbitmapはvisible rows + bounded overscanだけをrealizeし、selection、Favorite、Seen、Modal orderはcatalog identityへ保持する。
3. thumbnailはvisibleを最優先、次にoverscanとし、global worker数、in-flight重複、失敗再試行をboundedにする。Browserのversioned thumbnail cacheはread-onlyで再利用し、失敗時だけsource decodeへfallbackする。
4. filesystem enumerationと軽量catalog publicationをmetadata全件decodeより先に完了し、prompt/dimension metadataはcancel可能なbackground処理にする。
5. 日付sectionはCreated newest/oldestのときだけ表示する。Modified、Name、Randomでは表示しない。
6. ModalはBrowser既定のleft 28% / center 44% / right 28%を持ち、空の黒背景はclose、画像clickはchrome toggle、control clickはpropagateしない。
7. Favorite、Seen、Recent、Enhancement historyはmain checkoutの共有`.cache`を正本とし、linked worktree固有cacheへ分岐しない。既存fileを削除・移動しない。
8. Originalは固定2:3 card内contain、1:1はsquare cover、2:3はportrait coverとする。
9. Enhancementは明示クリックだけで、loopback Browser APIへ依頼する。passive scan/preview/modal navigationではenqueueもworker起動もしない。Browser不在はrecoverable inline statusにする。

## Safety boundaries

- user source image、`.cache`、viewer stateを削除・初期化しない。
- userが使うport 3000を停止・restart・占有しない。
- GitHub、GitHub Actions、deployment、外部AI相談を使わない。
- unrelated dirty fileとBrowser sourceを変更しない。
- verification fixture、state override、HTTP fakeはすべてTEMP内へ隔離する。

## Done gate

- exact 100,000 images / multi-folder focused smokeでcatalog、filtered、ItemsSource、tail selection、Grid/List/Modal、Created sectionが一致し、silent truncation 0。
- realized container数とthumbnail worker数がboundedで、dispatcher liveness gateを満たす。
- shared-root、aspect、Modal controls、Enhancement fake bridge、Delete neighbor、scan cancel/raceがgreen。
- WPF aggregate regressionと隔離した実画面確認がgreen。
- `docs/wpf-product-spec.md`、milestone recap、SQLite ledgerを更新し、local commitを作る。
