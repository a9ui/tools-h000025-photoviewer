# Displayed Original/Enhanced Enter + WPF List Enter-to-Modal

日付: 2026-07-20 JST

## Objective

Browser/WPFのModalで、external openとfile容量表示を現在displayed中のOriginalまたはEnhancedへ一致させる。WPF Grid/ListのEnterはcurrent filtered/sorted orderのprimary itemをModalで開き、close後はcurrent primary itemへfocusを戻す。

## Required contract

- Originalはactive catalog内source、Enhancedはsource identity/signatureとmanaged ownershipを満たすsucceeded outputを対象にする。
- missing/stale/invalid signature/ownership外Enhancedはsourceへfallbackし、recoverable statusとsource容量を出す。
- 容量はbytes / 1024²、小数2桁、spaceなしのexact `0.00MB`。toggle時に即更新する。
- capacity解決とexternal openは同じdisplayed-asset resolver/guardを使う。
- BrowserはGET/HEADをpassiveにし、POSTだけがshell launchする。WPFは`UseShellExecute`失敗をModalを閉じないRetry可能statusへ変換する。
- TextBox、ComboBox、DatePicker、Settings/Delete overlay、Modal native input、Landingから背面Enterを発火しない。buttonのnative Enter/Spaceは1回だけ実行する。
- EnterでEnhancement jobを作成せずworkerを起動しない。

## Preservation and boundaries

- WPF zoom/geometry anchor `e371b482af44e0428d9fe0d5217b236801f29cff`
- shared-state latency successor `5ae1e00`
- Browser/WPF shared Search History、GUID-isolated search stall、Favorite/Seen/Recent
- Modal transient UI/Filmstrip、Favorite/Enhanced status border、Browser/WPF parity laneはsemantic reconciliationし、重複実装しない。
- Browser port 3000、deployment、WinForms、unrelated dirty files、pushは対象外。
- GitHub Actionsと外部相談はgateにしない。AgmsgはOFF。

## Acceptance

1. Browser displayed-asset focused tests、typecheck、production buildがgreen。
2. WPF external-openとGrid/List Enter focused verifier、Release buildがgreen。
3. Search History focused/stall、aggregate + reload soakがgreen。
4. layout/state/stress verifierを変えていない場合は、前packetのexact100k/100folder証拠を継承できるdiff理由を残す。
5. final local mainを通常`start_wpf.bat`で起動し、`current / provenance-match`、exact one、`Responding=True`を確認する。
6. truth docs、GitHub #320、SQLite #47、closeout packetを更新し、次のCodex taskを実作成してGoalを閉じる。
