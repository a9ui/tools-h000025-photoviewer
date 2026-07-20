# WPF gallery zoom current-main promotion and normal-runtime acceptance

## Objective

`4a22b61`のWPF gallery zoom / geometry anchorをblind cherry-pickせず、task開始時のlocal
`refs/heads/main=4c81cca3efc80363568d0d9af35297ff3285b48c`と、その後にmainへ入った変更を保持してsemantic adoptionする。
focused、aggregate + reload soak、exact 100,000 / 100 folders、通常launcher/runtimeまで受け入れ、truth docsを`implemented`へ昇格する。

## Required preservation

- repository hardeningと`4c81cca3`以後のmain変更
- Browser/WPF shared Search History async/keyboard/a11y
- GUID-isolated `verify-wpf-search-stall.ps1`
- shared Favorite/Seen/Recent
- Browser port 3000、deployment、WinForms、unrelated dirty filesの非変更
- pushなし

## Acceptance

1. Release build 0 warnings / 0 errors。
2. `verify-wpf-gallery-zoom-anchor.ps1` green。
3. Search History focusedとsearch stall green。
4. `verify-wpf-product.ps1 -IncludeReloadSoak` green。
5. layout/state/stress差分を含むためexact 100,000 images / 100 foldersを再実行してgreen。
6. 通常`start_wpf.bat`で`current / provenance-match`、exact one process、`Responding=True`。
7. GitHub、SQLite、truth docs、closeout packet、次のCodex taskへ反映。

GitHub Actionsと外部相談はgateにしない。現行`AGENTS.md`でAgmsgはOFFのため使用しない。
