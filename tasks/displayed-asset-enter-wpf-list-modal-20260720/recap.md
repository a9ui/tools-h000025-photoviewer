# Recap — displayed asset Enter + WPF List Enter-to-Modal

日付: 2026-07-20 JST

## Outcome

Browser/WPFのModal capacityとEnter external-openは、現在displayed中のOriginal/Enhancedを同じresolverで解決する。WPF Grid/ListのEnterはcurrent filtered/sorted orderのprimary itemをModalで開き、navigation後closeでもcurrent primary itemへfocusを戻す。

- isolated clean checkpoint: `8ff1e5234202b1b1b17647fa79eac92c82af432e`
- adopted implementation: `a1d83c8`
- adopted WPF focus contract: `dbad550`
- adopted WPF native-button Enter isolation: `452ac02`
- final local-main revision: packet adoption後のexact revisionをlive GitHub #320とSQLite #47へ記録する

primary側のWPF Modal transient/Filmstrip/status-border変更とは、`MainWindow.xaml`と`MainWindow.xaml.cs`をresetせずsemantic reconciliationした。checkpointからadopted mainまで、今回所有する12 feature pathのblobは一致する。

## Implemented contract

- Browser `/api/open`はactive index/session membership、source type/existence、source identity/signature、managed outputのlexical/real final-path ownershipを検証する。
- GET/HEADはcapacity解決だけでpassive、POSTだけがshell launchする。
- WPFもcapacityとexternal openで同じdisplayed resolverを使い、canonical source/signature/managed ownershipを再検証する。
- Enhanced outputがmissing/stale/invalid signature/ownership外ならOriginalへfallbackし、source capacityとrecoverable statusを表示する。
- capacityは0 bytes、小容量、1MiB超を含めexact `0.00MB`。Original/Enhanced toggleで即時更新する。
- Grid/List Enterはcurrent filtered/sorted orderのprimary itemをModalで開く。close時はcurrent primary Grid/List itemへfocusを戻し、List recyclingとlogical selectionを維持する。
- Search、DatePicker、Settings/Delete overlay、Modal native input、Prompt chip button、Landingを隔離する。button Enter/Spaceはnative actionを1回だけ実行する。
- Enter/capacity解決はEnhancement jobを作らずworkerを起動しない。

## Verification

- Browser focused: `src/app/api/open/route.test.ts`、`src/lib/displayedAsset.test.ts`、`src/components/ImageModal.test.tsx`、`src/components/ImageGrid.test.tsx`、`src/store/ImageContext.test.tsx` — 5 files / 158 tests PASS。
- Browser `corepack pnpm typecheck`: PASS。
- Browser `corepack pnpm build`: PASS。port 3000は使用していない。
- WPF Release build: PASS、0 warnings / 0 errors。
- `verify-wpf-external-open.ps1`: Original/Enhanced targetと実容量、0/small/>1MiB formatter、missing/stale fallback、ownership拒否、shell failure、passive job stateがgreen。
- `verify-wpf-gallery-enter-modal.ps1`: Grid/List、current order、navigation、focus return、Search/Date/Settings/Delete/Modal input/Landing isolation、passive job stateがgreen。
- `verify-wpf-p1b.ps1`、`verify-wpf-prompt-tag-search.ps1`、`verify-wpf-modal-interaction.ps1`: green。
- Search History focused: green。
- GUID-isolated search stall: 5,000 images、final query exact、input 5ms、heartbeat 13/14、green。
- `verify-wpf-product.ps1 -IncludeReloadSoak`: 55/55 green、333,899ms。reload soak 24 cycles、40,322ms。log SHA-256 `B9D355CE876D5E1B15CE8B1FC8F95E5800B9238301E5D301BBC1034F70ABA0BC`。

## Exact 100k / 100 folders evidence

このmilestoneは`verify-wpf-catalog-stress.ps1`、`verify-wpf-gallery-zoom-anchor.ps1`、`verify-wpf-visual-layout.ps1`、`VirtualizingWrapPanel.cs`、`ViewerStateStore.cs`を変更していない。`App.xaml.cs`には新しいdisplayed-open/gallery-enter smoke modeと別laneのModal fixture互換だけを追加し、既存exact100k/100folder modeは変更していない。

そのため、前packet `tasks/wpf-gallery-zoom-current-main-promotion-20260719/recap.md`のexact 100,000 images / 100 folders証拠を継承した。catalog/filtered/Grid 100,000、tail 99,999、silent truncate 0、Grid/List realized 15/9、anchor drift 0、warm hit 100,000/miss 0、最大unresponsive 262ms / gate 750msである。今回のaggregateでもkey-binding exact 100,000 logical selectionと20,000 catalog stress/virtualizationがgreenである。

## Final normal launcher

packet adoption後のfinal local mainを通常`start_wpf.bat`で起動し、`check-wpf-launch-target.ps1`の`current / provenance-match`、exact one `PhotoViewer`、`Responding=True`、normal close後exact zeroをcloseout gateとする。repo内packetが自分自身の採用commit SHAを内包できないため、実測したexact revisionとPIDはlive GitHub #320とSQLite #47へ記録する。

## Preservation

- Browser port 3000、deployment、WinForms、GitHub Actions gate、push、Agmsgは使用していない。
- Search History、Favorite/Seen/Recent、zoom/geometry anchor、shared-state latency successorを維持した。
- primary checkoutのunrelated dirty fileを変更していない。
