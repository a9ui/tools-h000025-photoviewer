# Album v1 execution plan

1. Freeze start provenance and protect both existing worktrees. Use local main
   `8914935` (tree-identical to #322 head) as the sole implementation baseline;
   never fall back to the old `origin/main`.
2. Record source-commit disposition and the shared contract before code changes.
3. Implement Browser shared store + operation API and focused race/corruption tests.
4. Implement WPF reader/writer using the identical document and lock contract,
   then add a cross-runtime interleaving verifier.
5. Add Browser library/picker/source navigation and dialog/shortcut isolation
   without regressing current Modal/Filmstrip/Search History/Delete/Favorite.
6. Add WPF basic library/picker/source navigation with the same operations and
   unavailable/tombstone semantics; do not touch WinForms.
7. Run focused and full Browser/WPF gates, isolated Browser runtime, normal WPF
   launcher/reload, exact 100k, and failure/concurrency verifiers.
8. Update specs, ledger, current truth, GitHub/SQLite durable evidence and commit
   a green local-main checkpoint.
9. Keep #322 GitHub operations MERGE FROZEN. This does not exclude its already
   adopted content. On the final adopted head rerun all tree/provenance proofs
   and authoritative runtime matrix. Push/merge/close remain explicit-user
   actions outside this plan.

## Status at checkpoint 2

Steps 1-7 are green on the current local-main worktree. Browser full unit,
typecheck, lint, build and isolated Album Playwright passed. WPF focused Album,
56-check aggregate with reload soak, and exact 100,000 catalog passed. Step 8
is the active durable-doc/GitHub/SQLite checkpoint; step 9 remains intentionally
frozen because local main is now ahead of PR #322 head.

## Closeout status

Steps 1-8 are complete. The normal Browser launcher was also exercised on an
isolated explicit port with the clean implementation SHA and Album Playwright,
then the user-facing port 3000 was restored through the standard launcher and
verified loopback-only with every referenced static asset returning 200.
Step 9's proof shows that #322 is stale: local main is a two-commit implementation
descendant before this docs-only closeout, while the PR head remains `8914935`.
The proof is a freeze result, not authorization to push, merge, or close.

## Checkpoint 3 review-hardening status

The earlier closeout remains historical evidence for the committed Album v1
surface. A new independent-review hardening delta is now integrated in the
existing local `main` worktree above `f64d98d`; no new branch/worktree was
created. It hardens legacy Album migration, live-owner lock recovery,
same-state cleanup retry, unknown key-binding collision migration, and direct
non-shell external open. It also implements the requested Browser/WPF vertical
Filmstrip, Modal context actions, Settings/backdrop/hover changes, and the WPF
full-window Modal, gallery input, direct Favorite, window chrome, tooltip,
solid-cyan status, and prompt-display corrections.

Browser full unit is green at 69 files / 635 tests and Browser typecheck, lint,
and production build are green. WPF focused store/UI verifiers and Release build
are green. The remaining focused and normal-scale full Browser/WPF regressions,
loopback/Origin-Host guard tests, isolated Browser runtime, normal WPF
runtime/provenance, final SHA, GitHub, and SQLite reflection are **PENDING FINAL
GATE**. The user explicitly excluded 20,000/100,000 catalog-scale runs and
aggregate check-count tracking from this hardening checkpoint; prior green
records remain historical and require no rerun here. Step 8 is therefore
reopened only for this hardening delta; it must not be called complete until the
applicable gates finish. Step 9 remains **MERGE FROZEN** with no push, merge, or
close.

The attached public-readiness review is also reflected in this checkpoint.
Direct argument-vector external open is adopted. Explicit loopback binding for
`dev`/`start` and the mutating local API Origin/Host guard are in the worktree
with tests pending. Because `LICENSE` is unresolved and a full Codex Security
scan was not run under this no-external-consultation lane, repository public
readiness remains **NO-GO** and operation remains private/local with no
deployment.
