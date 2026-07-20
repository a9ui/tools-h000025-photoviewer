# Browser / WPF 全要望 parity ledger

監査日: 2026-07-20 JST

この表は会話中の追加要望を落とさないための実装台帳である。`implemented`はcurrent local mainのsourceとfocused/aggregate evidenceがある時だけ使う。`in progress`は未commitまたは片surfaceだけ、`pending`はcurrent mainへ製品実装がない状態である。

## 1. 正本・運用・回帰防止

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Browserを製品意味の正本にし、WPFをほぼ同機能にする | implemented contract | implemented contract, individual rows remain | `docs/photoviewer-authoritative-spec.md`と本ledgerを同時更新。Browser-only完了は禁止 |
| 通常launcherが古いbuild/branchを配信しない | provenance launcherあり | provenance launcherあり | `start_viewer.bat` / `start_wpf.bat`で最終HEAD一致をlive確認。port 3000はユーザーruntime |
| GitHub Actionsなしでもlocal gateとGit/GitHubでversion管理 | implemented local workflow | implemented local workflow | Actionsをgateにせず、focused/full verifier、commit、Draft PR/issue、SQLiteを使う |
| 古いbranchに埋もれた機能を回収する | Album意味監査済み、shared core採用 | shared core採用 | Claude Album MVPをPARTIAL、旧viewer commitをREJECT。blind cherry-pick禁止。`tasks/album-v1-semantic-recovery-20260720/audit.md` |
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
| Search HistoryをBrowser/WPFで共有 | implemented `0802f95` | implemented | v1/max50/NFKC identity、unknown保持、cross-runtime lost 0。同一Node/targetはprocess-local FIFOでも各turn shared lockを取り、30並列全200/lost 0、full unit 584/584 |
| key settingsをWPFにも持つ | implemented Browser | implemented WPF | editable bindings、conflict guard、hot apply/reload/reset、input/overlay isolation |
| settings write失敗を保存済みに見せずRetryできる | implemented | implemented core | malformed/future/lock保持、draft/value rollback、recoverable status |
| Favorite/Enhanced枠設定の同時保存でlost updateしない | implemented `805d6c9` + `343a364` | implemented `60ed739` | dirty preference単位でlock内latest diskへmerge。空patch拒否、他方の同時更新・unknown field・旧hexを保持し、malformed/future/busyは非破壊 |

## 5. 一覧status表示

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Favorite画像のthumbnailに設定可能な色枠、default黄 | implemented `2863519` + `805d6c9` | implemented `a091ec7` + `60ed739` | independent enabled/color、Favorite 2px inner ring、O(1)、Grid/List、selection/unseen非干渉、preference単位latest merge |
| AI高画質化済みthumbnailに設定可能な別色枠、default虹色 | implemented `2863519` + `805d6c9` | implemented `a091ec7` + `60ed739` | Enhanced 3px outer ring。`rainbow` presetまたは単色を選択可能。Favorite黄枠と同時可視、追加image I/O/全走査なし |
| 2種の枠を独立ON/OFF・表示色保存 | implemented `805d6c9` + `343a364` | implemented `60ed739` | shared `.cache/settings.json` nested unknown保持、旧hex値互換、empty patch拒否、malformed/future/busy非破壊、cross-surface lost update防止 |

## 6. Modal・Filmstrip・shortcut

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Modal画像を確実に表示し、session失効時に自動回復 | implemented | N/A: local file | Browser image410 coalesce scan/fresh token。WPFはdecode/source recoveryで同じ結果 |
| 左右edge click/Arrowでfiltered order移動 | implemented | implemented | user-configurable edge ratio、wrap、stale navigation拒否 |
| Delete後は隣へ正確に1枚だけ進む | implemented | implemented | Browser component+provider regression、WPF pre-delete order reconciliation |
| backdrop黒余白clickで一覧へ戻る | implemented | implemented | image/controls内clickはcloseしない |
| manual UI visibleは操作停止でも出たまま | implemented `f6f63d3` | implemented `a091ec7` | auto-hideをmanual stateと分離 |
| manual UI hiddenはcursorも隠し、pointer/keyで約900msだけ再表示 | implemented `f6f63d3` | implemented `a091ec7` | transient終了後cursor再非表示、touch/native input隔離 |
| UI hiddenは画像遷移/Delete/Original-Enhanced切替後も維持 | implemented `f6f63d3` | implemented `a091ec7` | Modal session stateとして保持 |
| zoom倍率を上端の目立ちにくい位置へ表示 | implemented `f6f63d3` | implemented `a091ec7` | root上端、低contrast、画像/Filmstrip非干渉 |
| Modalに現在表示中assetのfile容量を`0.00MB`形式で表示 | implemented `a1d83c8` | implemented `a1d83c8` | 1024² bytes、小数2桁・spaceなし。Originalはsource、Enhancedはmanaged output。toggle即更新、missing/stale/invalid signature/ownership外はOriginal容量 + recoverable status |
| manual Filmstripは画像外の専用bottom rowでviewportを縮める | implemented | implemented `a091ec7` + `c3d4ff5` | 画像へ重ねない、T/toolbarで開閉しWPF ViewerState/Browser view stateへ保存 |
| UI hidden中は下端hoverで前面overlay Filmstrip、離脱で収納 | implemented `f6f63d3` | implemented `a091ec7` | overlay時image geometry不変、current追従、bounded virtualization |
| top UI button後もArrow/Delete/T/H/F等shortcutが動く | implemented `f6f63d3` | implemented `a091ec7` + `452ac02` | Enter/SpaceはOriginalSource/Keyboard focusの両方でbutton/Prompt chip native activationを1回だけ実行、TextBox等は隔離 |
| H/D/F等をsimple monochrome icon化、Fはheart、Deleteはtrash | pending | pending | tooltip/aria-label/shortcut hintを残し、色だけに依存しない |

## 7. Delete・Windows action・clipboard

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| Deleteは永久削除でなくRecycle Bin | implemented | implemented | source/root/catalog/type/existenceを破壊境界直前に再検証 |
| Favorite Lv1〜5画像Deleteは通常設定に関係なく確認 | implemented | implemented | mandatory dialog、Do not ask again不可、bulk partial安全 |
| 一覧Enterで選択画像をModal表示 | implemented Browser | implemented `a1d83c8` + `dbad550` + `452ac02` | current filtered/sorted order、navigation後closeもcurrent primary Grid/List itemへfocus。Search/Date/Settings/Delete/Modal input/Landing隔離、button native Enter保持 |
| Modal Enterで現在表示中のOriginal/Enhancedをexternal open | implemented `a1d83c8` | implemented `a1d83c8` | Browser POSTだけlaunch、GET/HEAD passive、active index/type/existence、lexical+real managed ownership、shell failure。WPF canonical/signature/ownership guard + ShellExecute failure。missing/stale/invalid/ownership外はOriginal fallback |
| 右click menu: 画像を開く/Explorer/Path copy/Image copy | pending | pending | mouse+keyboard invocation、selection rule、disabled/error/focus/Escape |
| Ctrl+Cで画像をcopy | pending | pending | text input中はnative copy。bitmap+file-drop contractとclipboard failure |

## 8. AI高画質化

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| AI高画質化は明示actionだけで開始 | implemented | implemented Browser-API delegation | browse/preview/navigation/Favorite/Deleteでjob/workerを起動しない |
| WPFの拡大画面/一覧からEnhance actionを使える | implemented Browser | implemented delegation, live UI parity再検証中 | create/history/Original-Enhanced state、button action focused verifier |
| Windows EBUSYでも完成outputを誤failedにしない | implemented `3654b88` focused | Browser ownerを利用 | rename retry→awaited copy、cleanup-only lock分離。fresh real-GPU rerunは未実施 |
| Original/Enhanced toggleと現在表示assetを全actionで尊重 | partial | partial | preview、容量、Enter/openは実装。context menu/Ctrl+C image copyはSection 7 pending |

## 9. Album / collection

| Requirement | Browser | WPF | Current truth / completion gate |
| --- | --- | --- | --- |
| folderとは別に任意画像をまとめるAlbum/tag-like collection | implemented v1 library/picker/source | implemented v1 basic library/source | Browserはguarded outside session、WPFはoutside明示unavailable。missing tombstoneを保持 |
| create/rename/delete/pin/cover/recent | operation API + library implemented | identical store operations + library implemented | opaque id、recent順、cover member guard、conflict時reloadを採用 |
| selected images add/remove、Album内filter/view/Modal | bulk picker、Album順Gallery/Modal/Filmstrip、別remove/Recycle | selection add/remove、current-only Album順filter/Modal、catalog復帰 | Recycle成功後だけmembership cleanup。Search結果配列をAlbumで上書きしない |
| Browser/WPF同時更新でlost updateしない | implemented | implemented | shared create-new lock、latest-disk mutation、revision conflict、atomic publish、unknown保持。barrier付き16+16同時writerでrevision 32、lost 0、residue 0 |

## 10. 完了判定

- `implemented both`でないrowは未完了として残す。
- Browser/WPFのUI機構が異なっても、user outcome、state meaning、安全性、失敗時の非破壊、性能gateは一致させる。
- 100点/完全は、全pending解消に加えてreal Windows keyboard、high contrast、200% DPI、screen reader、real GPU Enhancementのmanual evidenceが揃うまで使わない。
