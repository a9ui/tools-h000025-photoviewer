# Browser Modal filmstrip + Browser/WPF shared Search History recap

Date: 2026-07-19 JST

## Outcome

Local main adopted two bounded product changes on implementation baseline
`792716f9863dd145e028648405c29c340f7a4336`:

- Browser enlarged-view filmstrip: virtualized, sparse, current-following,
  directly clickable, independently hideable, key-toggleable, and persisted.
- Browser/WPF shared Search History: versioned MRU JSON, maximum 50 complete
  queries, deterministic cross-runtime identity, individual/whole deletion,
  protected malformed/future behavior, and concurrent atomic writers.

WinForms remains FROZEN. Source images, user state/cache, ordinary browsing,
Enhancement passive ownership, deployment, and GitHub Actions were not used as
mutation targets or gates.

## Adopted commits

- `44f9716` Browser virtualized Modal filmstrip
- `66d2c1e` filmstrip ArrowLeft/ArrowRight Modal navigation
- `e282061` Browser shared Search History schema/API
- `5d1e57e` Browser focus/click Search History UI
- `410f1a2` WPF shared Search History UI/store/cross-runtime gate
- `8d0442f` current WPF search HelpText contract
- `792716f` concurrent verifier output isolation

## Verified-current descendants

The original milestone evidence above remains historical. Later local-main
descendants refine that adopted behavior without changing the Search History
schema or the bounded filmstrip navigation model:

- `9d8acb0` makes the filmstrip a dedicated layout row below the image
  viewport. Opening it reduces the image viewport instead of overlaying the
  image; chrome auto-hides after 3 seconds of inactivity, pointer/keyboard
  activity restores it, a Delete confirmation keeps it visible, and the zoom
  indicator stays at the top of the image viewport.
- `3654b88` fixes the observed Windows Enhancement `EBUSY` false failure. A
  completed copy is now a successful publish independently of temporary-file
  cleanup, cleanup receives bounded retry, and a still-locked temporary is
  reported as residue instead of changing a valid destination into a failed
  job. The ncnn-vulkan adapter also avoids reopening the just-written temporary
  with Sharp solely to read metadata.
- `f6f63d3` replaces the temporary 3-second Browser chrome policy with manual
  visible/manual hidden state. Manual visible stays visible; manual hidden also
  hides the cursor and allows a roughly 900ms pointer/key reveal. Bottom hover
  uses a foreground filmstrip overlay without changing image geometry, while
  the manually visible strip keeps its dedicated bottom row.
- `a091ec7` brings the same manual/transient state, Filmstrip geometry, top
  zoom indicator, and focused-button shortcuts to WPF. `c3d4ff5` persists the
  WPF Filmstrip open state in ViewerState.
- `2863519` and `a091ec7` make Favorite and AI-enhanced thumbnail status
  borders cross-runtime configurable. Favorite remains yellow by default;
  AI-enhanced now defaults to a distinct rainbow outer border and retains old
  saved hex colors as single-color settings.

## Browser evidence

- unit: 61 files passed, 3 skipped; 521 tests passed, 3 skipped
- typecheck: PASS
- lint: 0 errors; one unrelated warning under an existing Claude worktree
- production build: PASS
- isolated production Playwright: 7/7 PASS
- Modal filmstrip production coverage includes sparse far navigation,
  Arrow round-trip, single-step Delete reconciliation, current follow,
  chrome isolation, close/reopen, and persistence
- feature browser console: 0 errors / 0 warnings

For the later `9d8acb0` layout refinement, a focused isolated production E2E
ran on `http://127.0.0.1:3001` so the normal user runtime on port 3000 was not
used. `e2e/viewer-modal-filmstrip.spec.ts` passed 1/1 and verified that the
filmstrip starts at or below the image viewport bottom, the zoom indicator is
above the strip, 3-second auto-hide/re-show works, and the browser console has
no reported problems. The temporary port-3001 server was stopped after the
test.

The later `f6f63d3` Browser refinement passed 8 focused files / 190 tests,
typecheck, scoped lint, and diff-check. The rainbow status-border successor
`2863519` passed 5 focused files / 161 tests, typecheck, scoped lint, and
diff-check. A second live Browser run was intentionally not used because the
normal profile can merge legacy localStorage into shared state; no source,
cache, or user state was deleted or restored speculatively.

For `3654b88`, the Enhancement-focused unit set passed 4 files / 23 tests,
including transient and persistent cleanup locks and a real copy failure. The
previous failed user jobs and cache were not rewritten or deleted. A fresh
real-GPU Enhancement rerun reproducing the original user action is still
pending, so these deterministic tests prove the error classification and file
publication logic, not end-to-end GPU success on this machine.

## WPF / shared-state evidence

- Search History focused verifier: PASS
- Browser/WPF concurrent Search History: 20 + 20 writes, 42 final entries,
  lost 0, Unicode dedupe PASS, unknown fields preserved, malformed/future
  protected, Busy writes 0, lock/temp residue 0
- Release build: 0 warnings / 0 errors
- final `verify-wpf-product.ps1 -SkipStress`: 50/50 PASS
- `verify-wpf-modal-interaction.ps1`: all manual/transient chrome, cursor,
  dedicated/overlay Filmstrip geometry, persistence, top zoom, focused-button
  shortcut, navigation/Delete/Original-Enhanced hidden-state checks true
- `verify-wpf-thumbnail-status-borders.ps1`: shared rainbow schema, old hex,
  independent save/reload/reset, unknown/malformed/busy protection, O(1)
  Grid/List bindings, and residue-free checks all true
- current Release builds: 0 warnings / 0 errors

One preceding aggregate run stopped at the existing focus/filter race because
`SearchLatestWins` was false once. The same focused 20-iteration verifier
immediately passed, and a complete second 50-check aggregate passed including
that gate. No threshold was relaxed and the transient failure is not hidden.

The prior exact 100,000-image / 100-folder gate remains the large-catalog
baseline. The separate WPF gallery-zoom task also produced candidate commit
`b0f9a0e97b7bdbe791d8cd990d1a7973b42bd6e5` with exact-100k evidence, but it is
not adopted by this milestone because it overlaps the new Search History WPF
files and requires a semantic integration gate.

## Normal launcher adoption

After this task pack is committed, the same current HEAD is adopted through the
existing default launchers only:

- `start_viewer.bat` without an explicit port, followed by
  `verify-browser-runtime.ps1 -Port 3000 -ExpectedRevision <HEAD>`
- `start_wpf.bat`, followed by `check-wpf-launch-target.ps1 -Json` and one
  responding normal-root Release process check

Volatile build IDs, PIDs, fingerprints, and exe hashes are closeout evidence,
not normative product specification. No cache/state deletion is part of this
route.

## Remaining boundary

The next implementation milestone is semantic adoption of the WPF 20px to exact
one-column and geometry-anchor candidate on top of this shared-history main,
then focused, aggregate, exact-100k, and normal-launcher verification. Other
pending product requests remain in the authoritative specification and must not
be called implemented from branch archaeology alone.

## GitHub reflection status

Draft PR #322 remains open and Draft. The user ran the canonical command
`git push origin main:codex/wpf-thumbnail-performance-20260719`, after this
Codex session could not perform the external write itself. Both `gh pr view`
and `git ls-remote` then returned
`14ebe6a16a89123e5dfd6e966324522094c1188c`, matching the local milestone
head. SQLite improvement item #45 is `done`. The default `origin/main` remains
old until an explicit merge. GitHub Actions run `29689487462` also completed
successfully for `14ebe6a...`, but it was supplementary evidence rather than
the milestone gate. No merge or deployment was performed.
