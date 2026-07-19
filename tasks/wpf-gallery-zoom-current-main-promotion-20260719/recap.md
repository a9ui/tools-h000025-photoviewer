# Recap — WPF gallery zoom current-main promotion

日付: 2026-07-19 JST

## Outcome

WPF gallery zoom 20〜600、600=1列、state migration、canonical path + viewport offset anchorをlocal mainへ採用し、truth docsを`implemented`へ昇格した。

採用実装は`e371b482af44e0428d9fe0d5217b236801f29cff`。task開始時mainは`4c81cca3efc80363568d0d9af35297ff3285b48c`だった。作業中にmainがModal/filmstrip layout `9d8acb0`、同じzoom adoption、Browser Enhancement cleanupまで進んだため、重複patchを重ねずcurrent-main treeへsemantic reconciliationした。

## Implemented contract

- thumbnail range 20〜600、step 20、reset/default 200
- 600 endpointはavailable widthに関係なく1列
- 旧40〜600保存値を維持し、20未満/600超/非有限だけclamp
- visible selectionを優先したcanonical full path + viewport offset anchor
- selectionなしはviewport center fallback
- zoom、Sidebar、right panel、window resize、DPI changeでanchor復元
- 同名別folderをbasenameで誤同一視しない
- List modeはzoom拒否、recycling virtualizationを維持

## Verification

- Release build: PASS、0 warnings / 0 errors
- focused zoom/anchor: PASS、243 images、20/600、600=1列、max realized 3、全geometry drift 0、List 9/10/8 bounded
- Search History focused: PASS、keyboard/a11y/protected schema、Busy writes 0、lock/tmp residue 0
- search stall: PASS、5,000 images、final query exact、input 3〜4ms
- promotion aggregate + reload soak: PASS、53/53、reload 24/24
- exact 100,000 images / 100 folders: PASS、catalog/filtered/Grid 100,000、silent truncate 0、Grid/List 15/9、tail 99,999、20/600 endpoints、600=1列、全anchor drift 0、warm hit 100,000 / miss 0、最大unresponsive 262ms / gate 750ms
- 後続shared-state latency改善`5ae1e00`後の`verify-wpf-product.ps1 -SkipStress`: PASS、51/51、261,430ms
- shared-state latency focused 3-rep × 2: 6/6 green。large Favorite p95 0.147〜0.190ms、Modal p95 4.814〜5.506ms、dispatcher max gap 29.427〜45.155ms
- 通常launcherはpromotion時`3654b88`で`current / provenance-match`、exact one process、`Responding=True`。最終docs descendantではcloseout担当がexact revisionで再確認する

## Shared-state successor delta

`5ae1e00`はFavorite/Seenの100k full-map persistenceをfirst-event固定300ms cadenceでcoalesceし、連続入力でも永続化を無期限延期しない。通常pumpは1 batch、close/reloadの明示Drainはqueueが空になるまで即時処理する。Favorite/Seen kernel直列化はper-windowに限定し、別window/rootを巻き込まない。Favorite変更時はfilterが有効な時だけ全件filter/sortを再適用し、同一Modal画像は再decodeせずlevel表示だけ同期する。

fault gateはexternal entry保持、stale completion、rollback/retry、malformed保護、reload/close drain、residue 0を確認した。static reviewにもblocking findingはなかった。

## Preservation

- Browser port 3000と既存Browser process treeをproduct gateに使っていない
- deployment、WinForms、GitHub Actions、pushは実行していない
- Search History、Favorite、Seen、Recentの共有schemaを変更していない
- user-owned `next-env.d.ts`のSHA-256 `7B550DDA9686C16F36A17BF9051D5DBF31E98555B30D114AC49FC49A1E712651`を維持した
- current `AGENTS.md`でAgmsg OFFのため、Agmsgは実行していない

最終docs revisionの通常launcher provenance、GitHub issue #320、SQLite improvement item #46はcloseout担当がlive確認して記録する。
