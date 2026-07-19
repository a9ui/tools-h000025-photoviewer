# Browser / WPF 全要望 parity ledger

監査日: 2026-07-19 JST

この表は会話中の追加要望を落とさないための実装台帳である。`implemented`はcurrent local mainのsourceとfocused/aggregate evidenceがある時だけ使う。`in progress`は未commitまたは片surfaceだけ、`pending`はcurrent mainへ製品実装がない状態である。

## 1. 正本・運用・回帰防止

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Browserを製品意味の正本にし、WPFをほぼ同機能にする | implemented contract | implemented contract, individual rows remain | `docs/photoviewer-authoritative-spec.md`と本ledgerを同時更新。Browser-only完了は禁止 |
| 通常launcherが古いbuild/branchを配信しない | provenance launcherあり | provenance launcherあり | `start_viewer.bat` / `start_wpf.bat`で最終HEAD一致をlive確認。port 3000はユーザーruntime |
| GitHub Actionsなしでもlocal gateとGit/GitHubでversion管理 | implemented local workflow | implemented local workflow | Actionsをgateにせず、focused/full verifier、commit、Draft PR/issue、SQLiteを使う |
| 古いbranchに埋もれた機能を回収する | audit中 | audit中 | Album branch `codex/album-library`のMVPを発見。blind cherry-pick禁止 |
| WinFormsは削除せず凍結 | FROZEN | FROZEN | `local-native/PhotoViewer.Native/**`は重大破損/起動不能だけ保守 |
| private/local製品として運用し、deploymentしない | implemented policy | implemented policy | Vercel/public distributionはscope外。repo visibility変更時は別途security live gate |

## 2. Landing・catalog・10万件性能

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| 複数folder/約100,000画像を開ける | partial: paged sparse catalog、固定製品上限なし | implemented | WPF exact100k/100folders、tail99,999、silent truncate 0。Browserは大規模実data相当のlong-scroll gateを追加 |
| 画像一覧に全fileのlogical一覧を先に持ち、末尾へ即移動できる | partial: server order + sparse page | implemented | WPF full `_tiles` + bounded realization。Browserは全DOM化せずlogical count/scroll extent/remote pagesを維持 |
| thumbnailはvisible最優先で速く、スクロール後に一から待たせない | implemented architecture, live large proof要更新 | implemented architecture | Browser LRU/pending dedupe/warm priority、WPF bounded decode/cache。same-fixture cold/warm/fast-tail測定 |
| Favorite-only等filterで途中停止・枚数上限を作らない | partial evidence | implemented | Browser全page境界long-scroll、Favorite Lv別/Allのexact tailをisolated E2Eで証明 |
| UI input/scroll/Favorite/Modalを重くしない | implemented focused | implemented focused | Browser virtualized render、WPF `5ae1e00` latency 6/6、aggregate 51/51。継続budget regression gate |
| date headerはCreated/Modified date sort時だけ。Random等で日付区切りしない | implemented | implemented | sort-mode/date-layout focused verifierを維持 |

## 3. Sidebar・filter・gallery zoom

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Quick Searchを置かない | implemented | implemented | regression guardで復活を拒否 |
| Today / 7d / 30d / This yearを置かない | implemented | implemented | manual From/Toだけ。legacy tokenはmigration readのみ |
| Foldersを畳めて永続化する | implemented | implemented | expanded/collapsed、focus/UIA、reload |
| Favorite Lv1〜5を独立ON/OFF、ON+levelなしはAll | implemented | implemented | exact OR、Unrated排他。WPF専用All buttonはergonomic candidate |
| Unseen blue dotを独立ON/OFF、toggleだけでSeenを書かない | implemented | implemented | default OFF、Grid/List両方、shared Seenとは別のpresentation設定 |
| zoomでsidebar/header/text/List rowを拡縮しない | implemented | implemented | gallery cardだけ20〜600、step20 |
| 最小はさらに小さい20、最大600で正確に1列 | implemented | implemented (`e371b48`) | persisted migration/clamp、List virtualization非破壊 |
| zoom時に選択/見えている画像をpath+viewport offset anchorにする | implemented | implemented (`e371b48`) | Sidebar/right panel/window/DPI、selection有無、同名別folder drift 0 |
| Sidebar collapsedでも同じanchor挙動 | implemented | implemented | geometry-change pathを1本化し、開閉有無で別挙動にしない |
| aspect 1:1等display styleが機能する | implemented | implemented | Original/1:1/2:3、Grid/List/current view persistence |

## 4. Favorite・Seen・Search History・設定共有

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Favorite履歴をBrowser/WPFで共有 | implemented | implemented | shared JSON、cross-runtime lock/atomic/merge、large writer |
| Seen履歴をBrowser/WPFで共有 | implemented | implemented | shared JSON、Unseen表示toggleとは独立 |
| Search input focus/clickで検索履歴list | implemented | implemented | whole-query replacement、keyboard/list selection、個別削除/Clear all |
| Search HistoryをBrowser/WPFで共有 | implemented | implemented | v1/max50/NFKC identity、unknown保持、concurrent lost 0 |
| key settingsをWPFにも持つ | implemented Browser | implemented WPF | editable bindings、conflict guard、hot apply/reload/reset、input/overlay isolation |
| settings write失敗を保存済みに見せずRetryできる | implemented | implemented core | malformed/future/lock保持、draft/value rollback、recoverable status |

## 5. 一覧status表示

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Favorite画像のthumbnailに設定可能な色枠、default黄 | implemented `837e722` | in progress | independent enabled/color、Favorite inner ring、O(1)、Grid/List、selection/unseen非干渉 |
| AI高画質化済みthumbnailに設定可能な色枠、default黄 | implemented `837e722` | in progress | Enhanced outer ring。Favoriteと同時可視、追加image I/O/全走査なし |
| 2種の枠を独立ON/OFF・色保存 | implemented | in progress | shared `.cache/settings.json` nested unknown保持、WPF同schema採用 |

## 6. Modal・Filmstrip・shortcut

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Modal画像を確実に表示し、session失効時に自動回復 | implemented | N/A: local file | Browser image410 coalesce scan/fresh token。WPFはdecode/source recoveryで同じ結果 |
| 左右edge click/Arrowでfiltered order移動 | implemented | implemented | user-configurable edge ratio、wrap、stale navigation拒否 |
| Delete後は隣へ正確に1枚だけ進む | implemented | implemented | Browser component+provider regression、WPF pre-delete order reconciliation |
| backdrop黒余白clickで一覧へ戻る | implemented | implemented | image/controls内clickはcloseしない |
| manual UI visibleは操作停止でも出たまま | implemented `f6f63d3` | in progress | auto-hideをmanual stateと分離 |
| manual UI hiddenはcursorも隠し、pointer/keyで約900msだけ再表示 | implemented `f6f63d3` | in progress | Delete confirm中はvisible、touch/native input隔離 |
| UI hiddenは画像遷移/Delete/Original-Enhanced切替後も維持 | implemented `f6f63d3` | in progress | Modal session stateとして保持 |
| zoom倍率を上端の目立ちにくい位置へ表示 | implemented `f6f63d3` | in progress | hover/focus時だけcontrast上昇 |
| manual Filmstripは画像外の専用bottom rowでviewportを縮める | implemented | in progress | 画像へ重ねない、T/toolbarで保存開閉 |
| UI hidden中は下端hoverで前面overlay Filmstrip、離脱で収納 | implemented `f6f63d3` | in progress | overlay時image geometry不変、current追従、bounded virtualization |
| top UI button後もArrow/Delete/T/H/F等shortcutが動く | implemented `f6f63d3` | in progress | Enter/Spaceはbutton native activation、TextBox等は隔離 |
| H/D/F等をsimple monochrome icon化、Fはheart、Deleteはtrash | pending | pending | tooltip/aria-label/shortcut hintを残し、色だけに依存しない |

## 7. Delete・Windows action・clipboard

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Deleteは永久削除でなくRecycle Bin | implemented | implemented | source/root/catalog/type/existenceを破壊境界直前に再検証 |
| Favorite Lv1〜5画像Deleteは通常設定に関係なく確認 | implemented | implemented | mandatory dialog、Do not ask again不可、bulk partial安全 |
| 一覧Enterで選択画像をModal表示 | implemented Browser | pending WPF | current filtered/sorted order、focus return、input/overlay isolation |
| Modal Enterで現在表示中のOriginal/Enhancedをexternal open | pending | pending | managed output ownership/existence guard、missing時source fallback |
| 右click menu: 画像を開く/Explorer/Path copy/Image copy | pending | pending | mouse+keyboard invocation、selection rule、disabled/error/focus/Escape |
| Ctrl+Cで画像をcopy | pending | pending | text input中はnative copy。bitmap+file-drop contractとclipboard failure |

## 8. AI高画質化

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| AI高画質化は明示actionだけで開始 | implemented | implemented Browser-API delegation | browse/preview/navigation/Favorite/Deleteでjob/workerを起動しない |
| WPFの拡大画面/一覧からEnhance actionを使える | implemented Browser | implemented delegation, live UI parity再検証中 | create/history/Original-Enhanced state、button action focused verifier |
| Windows EBUSYでも完成outputを誤failedにしない | implemented `3654b88` focused | Browser ownerを利用 | rename retry→awaited copy、cleanup-only lock分離。fresh real-GPU rerunは未実施 |
| Original/Enhanced toggleと現在表示assetを全actionで尊重 | partial | partial | preview表示は実装。Enter/open/context/copyはSection 7 pending |

## 9. Album / collection

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| folderとは別に任意画像をまとめるAlbum/tag-like collection | pending semantic recovery | pending | 旧branch `codex/album-library` commit `d2a99c4`を発見。current main未採用 |
| create/rename/delete/pin/cover/recent | old branch MVP only | disabled placeholder | v1 model、shared ownership、UI、migration、focused testsが必要 |
| selected images add/remove、Album内filter/view/Modal | old branch MVP only | pending | full current catalog identity、missing/moved source表示、bulk semantics |
| Browser/WPF同時更新でlost updateしない | missing old MVP | missing | 旧`.cache/albums.json` writerはlock/atomic/failure status/testがないため、そのまま採用禁止 |

## 10. 完了判定

- `implemented both`でないrowは未完了として残す。
- Browser/WPFのUI機構が異なっても、user outcome、state meaning、安全性、失敗時の非破壊、性能gateは一致させる。
- 100点/完全は、全pending解消に加えてreal Windows keyboard、high contrast、200% DPI、screen reader、real GPU Enhancementのmanual evidenceが揃うまで使わない。

