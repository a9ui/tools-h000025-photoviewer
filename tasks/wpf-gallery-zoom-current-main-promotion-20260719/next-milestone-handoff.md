# Next milestone handoff — displayed Original/Enhanced Enter + WPF List Enter-to-Modal

## Goal

Browser/WPFでEnterによる外部openを「現在表示中のOriginalまたはEnhanced」に一致させ、WPF Grid/ListのEnterでcurrent-order Modalを開く。input/overlay/key-binding isolation、focus return、path guard、missing output fallbackをfocused gateで固定する。

## Start state

- 起点はtask作成時のlocal `refs/heads/main`。`origin/main`を使わない
- 前milestone実装: `e371b482af44e0428d9fe0d5217b236801f29cff`
- shared-state successor: `5ae1e00`
- 前milestonepacket: `tasks/wpf-gallery-zoom-current-main-promotion-20260719/**`
- truth docs: `docs/photoviewer-authoritative-spec.md`、`docs/current-implementation-truth.md`、`docs/wpf-product-spec.md`

## Required semantics

1. Enter external-openはsource固定ではなく、各surfaceの現在displayed assetを使う。
2. Enhanced表示中でもmanaged outputがmissing/stale/outside ownershipならsourceへ安全fallbackし、recoverable statusを出す。
3. Browser/WPFともcanonical path、active root/catalog、file existence/type、shell launch failure guardを維持する。
4. WPF Grid/ListのEnterはprimary selectionをcurrent filtered/sorted orderのModalで開く。
5. TextBox、ComboBox、DatePicker、Settings/Delete overlay、Modal内native input、Landingでは背面Enter actionを発火しない。
6. Modal close後は元のGrid/List itemへfocusを戻す。List recycling/100k logical selectionを壊さない。
7. EnterでEnhancement jobを作成せず、workerを起動しない。

## Verification minimum

- focused displayed Original/Enhanced open、missing/stale fallback、shell failure
- focused WPF Grid/List Enter、current order、focus return、input/overlay/Landing isolation
- Release build
- Search History focused/stall
- aggregate + reload soak。layout/state/stress verifierを変えた場合はexact100k/100folderも再実行
- 最終local mainの通常launcher `current / provenance-match`、exact one `Responding=True`

## Boundaries

- Browser port 3000、deployment、WinForms、unrelated dirty filesを触らない
- Actionsと外部相談をgateにしない
- pushは別途指示がない限り行わない
- current `AGENTS.md`でAgmsgはOFF
- ユーザー追加のModal transient UI/Filmstrip、Favorite/Enhanced枠、Browser/WPF parity laneと重複実装しない
- milestone完了時にtruth docs、GitHub、SQLite、closeout packetを更新し、次の新規Codex taskを作ってGoalを閉じる
