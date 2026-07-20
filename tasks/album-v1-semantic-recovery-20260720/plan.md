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
