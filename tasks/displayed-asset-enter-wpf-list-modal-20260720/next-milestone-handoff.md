# Next milestone handoff — image context menu + Ctrl+C image copy

## Goal

Browser/WPFに共通の画像context menuとCtrl+C image copyを追加する。現在displayed中のOriginal/Enhanced、selection、input isolation、clipboard failure、focus/Escape、a11yを同じ意味論へ揃え、通常browse/preview/copyからEnhancement jobやworkerを起動しない。

## Start state

- 起点はtask作成時のlocal `refs/heads/main`。stale `origin/main`を使わない。
- displayed asset resolver/adoption: `a1d83c8`とWPF後続focus/Enter isolation。
- WPF zoom/geometry anchor: `e371b482af44e0428d9fe0d5217b236801f29cff`。
- shared-state latency successor: `5ae1e00`。
- current milestone packet: `tasks/displayed-asset-enter-wpf-list-modal-20260720/**`。
- parity ledger: `tasks/browser-wpf-product-parity-20260719/requirements-ledger.md`。

## Required semantics to decide first

1. Grid/List/Modalの右clickとkeyboard invocationで同じproduct menuを出す。Browser既定context menuをproduct contractと見なさない。
2. action availabilityとselection ruleをBrowser/WPFで一致させる。未選択itemへのcontext invocationがprimary selectionを置換するかを明示する。
3. `Open externally`、`Show in folder`、`Copy path`、`Copy image`は既存canonical active-root/catalog/type/existence guardを共有する。
4. Modalではcurrent displayed Original/Enhancedを使う。invalid Enhancedはsourceへfallbackしrecoverable statusを出す。
5. Ctrl+C image copyのclipboard formatを明示する。bitmapとfile-dropの両方を出すか、surface差を設けるならuser outcomeを揃える。
6. TextBox/ComboBox/DatePicker/metadata inputではnative text copyを優先し、背面image copyを発火しない。
7. clipboard busy/permission/format failureはselection、focus、Modal、source/shared stateを壊さずRetry可能にする。
8. menuはmouse/keyboard、Escape、focus return、disabled item、screen-reader name/roleをfocused testで固定する。
9. copy/open/menu表示だけでEnhancement jobを作らずworkerを起動しない。

## Verification minimum

- Browser/WPF context menu mouse + keyboard invocation、selection rule、availability、Escape/focus return。
- Browser/WPF Ctrl+C image copy、displayed Original/Enhanced、fallback、clipboard failure、text/native input isolation。
- existing external open/path guardとDelete confirmationのregression。
- Release build、Search History focused/stall、aggregate + reload soak。
- layout/state/stress verifierを変えた場合だけexact100k/100folderを再実行し、変えない場合は証拠継承理由を記録する。
- final local mainの通常launcher `current / provenance-match`、exact one `Responding=True`。

## Boundaries

- Browser port 3000、deployment、WinForms、unrelated dirty filesを触らない。
- Actionsと外部相談をgateにしない。pushは別指示がない限り行わない。
- current `AGENTS.md`でAgmsgはOFF。
- Album/collectionはこのmilestoneへ混ぜない。
- milestone完了時にtruth docs、GitHub、SQLite、closeout packetを更新し、次の新規Codex taskを作ってGoalを閉じる。
