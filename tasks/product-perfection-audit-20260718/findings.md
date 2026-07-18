# Findings

## Closed in this milestone

- Browser Settingsは共有saveをHTTP成功、`ok: true`、要求値の完全echo後だけcommitする。409/503/network/invalid responseではkey-binding draftを保持し、delete-confirm toggleを保存値へ戻してRetryを残す。同一fieldのsaveはFIFO、別fieldは独立し、late initial GETはfield-local generationとdirty actionで安全にmergeする。
- Browser desktop SidebarのSize rowは240px columnを押し広げない。slider shrinkとhorizontal overflow guardをsource/test/runtimeで固定した。
- Browser Modal Deleteは削除前orderの隣を1回だけcommitする。3枚中央削除後、late search refreshが来ても2枚分進まないcomponent + provider統合回帰を追加した。
- Browserで成功した別catalogへの切替だけが旧selection/modal/reveal/tab snapshotをclearする。same catalog、failed scan、cancelは保持する。
- WPFのall-unavailable新intentは受理時点で旧scanをsupersedeし、遅延publish/state/Recent ownershipを拒否する。
- WPF 20,000件はbackground existence snapshot、single Reset publish、bounded/frozen brush、O(visible) realizationへ置換した。far-tail canonical selection、実container highlight、Grid→List→Grid往復も同じpathを維持する。
- WPFのbounded Grid indexをfull List indexへ誤適用するmode-switch回帰を総合reload soakで発見し、canonical path同期へ修正した。hidden surfaceは列挙せず、表示中の実体化済みcontainerだけをgeneration付きで同期する。
- WPF FoldersはUI Automation `ExpandCollapse`を実装し、Expanded→Collapsed→Expandedとpersist/migrationを実processで固定した。
- WPF 100,000-entry Favorite/Seenの同期save P1は、独立generation-aware writerへ移行した。coalescing、latest-disk merge、atomic replace、generation rollback、composite Retry、reload barrier、close drain/reopenをfault injectionで固定した。
- WPF latency gateは絶対50/65/110msを維持し、相対gateを`large <= max(control × 2.5, control + 10ms)`としてsub-frame scheduler jitterを明示的に扱う。self-testは5→20msと旧186/251/460ms baselineをREDにする。

## No known live P0/P1

監査したprivate-local coreに既知のlive P0/P1はない。これは全machine/input/assistive technologyに対する無謬性の主張ではない。

残りは次の境界:

- P2 measure-first: Browser large Favorite/Seen full-map write、large sparse preview restore、client filter page boundary。
- P2 product choice: WPF double-click tab creation、empty tab strip、configurable keys/shared delete-confirm ownership、Favorite `All`専用button、2D zoom anchor refinement。
- P2 test-only: smoke専用`ImportPvu*` helperのactor-pending guard。現product UI callerはない。
- Manual evidence: Narrator/NVDA、高contrast、200% text/DPIの連続journey。sourceのUIA/focus testやscreenshotだけで合格とはしない。
- P3: WPF Enhancement ownership、cache quota/eviction、installer/signing/update。private-local coreと分離する。

## Final runtime evidence

- Browser unit: 54 files PASS / 2 skipped、445 tests PASS / 2 skipped。
- Browser typecheck、lint、production build、UI regression guard 18 files: PASS。
- Browser Playwright: 3/3 PASS。
- WPF aggregate `-IncludeReloadSoak`: 46/46 PASS、241,988ms。
- WPF exact catalog: 20,000/20,000、silent truncate 0、Grid max 384、List 22、far-tail canonical/visual/mode round-trip all true、round-trip 32ms、dispatcher 292ms、external 254ms < 750ms。
- WPF 100,000-entry latency 3/3: Modal p95 7.159/9.803/12.169ms、Favorite p95 45.148/41.155/42.445ms、dispatcher gap 50.561/50.412/55.704ms。large close lifecycleは全回pending→deferred→drained、final pending 0。
- WPF shared writer fault/close matrix、Folders UIA、24-cycle reload soak: PASS。
- Browser/WPF shared Favorite/SeenとRecent cross-runtime stress: 各20 iterations PASS、valid JSON、lost update 0、lock/tmp residue 0。
- WPF visual verifier: 1280×820 / 1024×700のLanding、Viewer、Settings、Folders collapsed、Unseen on全10枚を生成し目視確認。破綻/cropは見つからないが、screenshotはaccessibility証拠ではない。
