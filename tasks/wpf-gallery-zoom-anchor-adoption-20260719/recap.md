# WPF gallery zoom / geometry anchor adoption recap

Date: 2026-07-19 JST

## Result

The implementation and verification are complete on the dedicated branch
`codex/wpf-gallery-zoom-anchor-adoption-20260719` at implementation commit
`4a22b61`. It is a branch-ready adoption candidate, not yet a local-main
adoption.

The branch was created from the required local-main commit
`492988ba2390172c3331b72762f5cecd907a3605`. During this work another lane moved
`refs/heads/main` to hardening descendant
`4c81cca3efc80363568d0d9af35297ff3285b48c`. This branch was intentionally not
rebased: promotion must preserve that hardening and reconcile overlapping truth
docs semantically.

## Adopted behavior on this branch

- Grid zoom range 20 through 600, step 20, reset/default 200.
- Exact one-column layout at 600 through an explicit virtual-panel contract.
- Safe legacy-state migration: valid 40 remains 40, legacy 5 clamps to 20, and
  unknown fields survive.
- Canonical full-path plus viewport-offset anchor, with selected-visible-first
  and viewport-center fallback behavior.
- Anchor restoration after zoom, Sidebar toggle, right-panel resize, window
  resize, and DPI change, with selected and unselected cases.
- List-mode zoom rejection while retaining bounded recycling virtualization.
- A focused smoke/verifier route using GUID-isolated TEMP state and duplicate
  basenames in different folders.

## Verification evidence

- Release build: PASS, 0 warnings and 0 errors.
- Focused gallery zoom/anchor verifier: PASS with 243 images; 20 and 600
  endpoints; 600 columns = 1; realized items bounded; duplicate-basename anchor
  resolved by canonical full path; all zoom/Sidebar/right-panel/window/DPI
  selected and unselected drifts = 0; legacy migration and unknown-field
  preservation PASS.
- Current aggregate: `verify-wpf-product.ps1 -IncludeReloadSoak` PASS,
  `ok=true`, 53/53 checks, elapsed 353,427 ms. Reload soak passed 24/24.
- Exact stress: 100,000 images / 100 folders PASS; silent truncation 0; tail
  index 99,999; Grid/List realized 15/9; 600 endpoint columns = 1; anchor drifts
  0; warm cache hits 100,000, misses/writes 0; shared cache SHA and mtime
  unchanged; cleanup succeeded; elapsed 91,599 ms.
- Search History focused verifier: PASS for popup/focus/click, keyboard
  Arrow/Enter, accessibility announcement, protected schema, Busy writes 0,
  and residue 0.
- Search-stall verifier: PASS at 5,000 items, input 3 ms, heartbeat/final query
  PASS.
- Browser/WPF Search History concurrent 20+20: PASS with 42 entries, Unicode
  dedupe, unknown-field preservation, malformed/future protection, Busy writes
  0, and residue 0.
- Shared Favorite/Seen and Recent concurrent 20-iteration gates: PASS with
  residue 0 and unknown-field preservation where applicable.
- Shared-state latency: final isolated 3/3 PASS and final full aggregate PASS.

Two transient red observations were superseded and are retained rather than
hidden: an early cross-runtime Search History run reported WPF
focus/keyboard/accessibility false, then the immediate focused rerun and final
aggregate passed; a latency sample measured 65.3 ms against a 65 ms boundary,
then isolated 3/3 and final aggregate passed under the same contract.

## Preservation and exclusions

- The baseline Search History hunks in `App.xaml.cs`, `MainWindow.xaml`, and
  `MainWindow.xaml.cs` have zero diff, and
  `scripts/verify-wpf-search-stall.ps1` is byte-for-byte unchanged from the
  required baseline.
- All runtime fixtures used GUID-isolated TEMP roots. The normal Browser root,
  port 3000, normal WPF launcher/runtime, user state/cache/source, WinForms, and
  deployment were not touched.
- GitHub Actions and external AI consultation were not used as gates.
- Nothing was pushed.

## Closeout status

The code milestone is green on its dedicated branch. Product truth remains
`pending adoption` until the next task semantically promotes the commit onto
the then-current local main and verifies the normal WPF launcher/runtime.

The required Agmsg closeout pointer was attempted through the official skill,
but both this worktree and the normal project path returned
`not_joined=true available_teams=none`. The skill forbids inventing a team,
identity, or delivery mode without user setup, so no message was sent and the
explicit non-gating consultation rule was honored. The next Codex task was
queued as `client-new-thread:f559d101-3e53-4bc1-bea3-66baf0464327` from local
main with this handoff in its initial prompt.
