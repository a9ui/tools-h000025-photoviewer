# Browser / WPF product-perfection closeout

Date: 2026-07-18 JST
Scope: private/local Browserを正本にしたBrowser全体監査とWPF重点hardening
External AI / GitHub Actions / deployment: not used

## Outcome

監査した主要journeyに既知のlive P0/P1はない。Browserは正本仕様を維持し、WPFは日常viewerの意味を高い水準で再現しつつ、Windows-native safety/recoveryと20,000件full catalogを提供する。これは「絶対に未知の欠陥がない」という主張ではない。manual assistive-technology evidenceと明示したP2/P3は残す。

Eight-axis auditはBrowser 4.6/5（92/100）、WPF 4.6/5（92/100）。private-local coreの現baselineとして採用する。配布、署名、自動updateを含む市販packageのscoreではない。

## Browser changes

- shared Settings saveはHTTP success + `ok:true` + requested value echo後だけcommitする。
- 409/503/network/invalid responseはdraft/保存値を保ち、inline Retryを出す。
- same-field save FIFO、field-local initial GET generation、key action単位dirty mergeでlate responseを安全に処理する。
- desktop Sidebarを240px固定のままhorizontal overflowさせない。
- Modal Deleteは削除前orderの隣へ1回だけ移り、late search refreshで2枚分進まない。
- 成功した別catalog adoptionだけが旧catalogのvolatile selection/modal/reveal/tab snapshotをclearする。

## WPF changes

- all-unavailable新scan intentも旧runを即supersedeする。
- 20,000件catalogをbackground existence snapshot + single Reset publish + bounded Grid/Listでexactに扱う。
- far-tail selectionをcanonical path正本にし、実containerのhighlightとGrid→List→Grid往復をO(visible)で維持する。
- bounded Grid indexをfull List indexへ誤用して別画像を選ぶ総合soak検出回帰を修正した。
- 1 MiB+ Favorite/Seenを独立generation-aware writerへ移し、latest-disk merge、atomic replace、coalescing、rollback/Retry、reload barrier、close drain/reopenを実装した。
- Folders headerはnative UI Automation `ExpandCollapse`を公開する。
- verifier自身もTEMP output boundary、exact process tree、timeout、cleanup実成否、close lifecycleを検証するよう補強した。

## Accepted evidence

### Browser

- unit: 54 files PASS / 2 skipped、445 tests PASS / 2 skipped。
- `pnpm typecheck`: PASS。
- `pnpm lint`: PASS。
- `verify-ui-regression-guard.ps1`: 18 files PASS。
- Playwright: 3/3 PASS。
- `pnpm build`: PASS。
- isolated production runtimeは通常port 3000と分離し、loopback、served revision/build、console、live interactionを確認する。通常port 3000はstop/restart/adoptしない。

### WPF

- `verify-wpf-product.ps1 -IncludeReloadSoak`: 46/46 PASS、241,988ms。
- 20,000件: catalog/filtered 20,000、silent truncate 0、Grid max 384、List 22、far-tail canonical/visual/mode round-trip all true、round-trip 32ms、dispatcher 292ms、external 254ms < 750ms。
- 100,000-entry 3 runs: Modal p95 7.159/9.803/12.169ms、Favorite p95 45.148/41.155/42.445ms、dispatcher max gap 50.561/50.412/55.704ms。
- latency absolute gate 50/65/110ms、relative gate `large <= max(control × 2.5, control + 10ms)`、旧P1/self-test rejection: PASS。
- writer fault/close/reload matrix: PASS。
- reload soak 24/24、final folder/selection/modal/tab current、stale 0、memory/store/source isolation: PASS。
- shared Favorite/Seen cross-runtime 20 iterations: PASS。
- shared Recent cross-runtime 20 iterations: PASS。
- exact viewport visual states 10枚を生成して目視した。破綻/cropは見つからないが、screenshotはaccessibility proofではない。

## Safety boundary

- user port 3000を停止・再build・adoptしない。
- user state/cacheを削除しない。
- actual source imageをDeleteしない。Recycle/fault testはTEMP fixtureだけ。
- local-native WinForms、Enhancement worker ownership、deployment、unrelated dirty filesを変更しない。
- GitHub/Actionsをgateにしない。

## Remaining explicit boundary

### P2 / measure or decide first

- Browser large Favorite/Seen full-map write、large sparse preview restore、client-filter page boundary。
- WPF double-click tab creation、empty tab strip、configurable key editor、shared delete-confirm ownership、Favorite `All`専用button、2D zoom anchor refinement。
- smoke-only `ImportPvu*` helperのactor-pending guard。現product UI callerはない。
- 1024px compact layoutのvisual polish。

### Manual evidence

- Narrator/NVDA、高contrast、200% text/DPIの連続journey。

### P3 / explicit product decision

- WPF Enhancement enqueue/worker/cancel/retry/output delete。
- cache quota/eviction。
- installer、signing、auto-update、public distribution。

## Recommended next three bounded milestones

1. WPF compact layout + manual accessibility evidence。
2. Browser large-state measurement and only evidence-driven optimization。
3. WPF non-destructive external-change inbox + explicit Refresh。

Normative detailは`docs/browser-feature-contract.md`と`docs/wpf-product-spec.md`、full parity/scoreは`browser-wpf-parity-audit.md`、finding closureは`findings.md`を参照する。
