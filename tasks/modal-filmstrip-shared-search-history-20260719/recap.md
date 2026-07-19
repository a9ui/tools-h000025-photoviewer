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

## WPF / shared-state evidence

- Search History focused verifier: PASS
- Browser/WPF concurrent Search History: 20 + 20 writes, 42 final entries,
  lost 0, Unicode dedupe PASS, unknown fields preserved, malformed/future
  protected, Busy writes 0, lock/temp residue 0
- Release build: 0 warnings / 0 errors
- final `verify-wpf-product.ps1 -SkipStress`: 50/50 PASS

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
