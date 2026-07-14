# Browser Whole Review - Recap

## Outcome

The browser surface was reviewed from the normal production launcher, source, tests, persistence contracts, and in-app Browser interaction. Reproducible browser regressions were fixed without deleting user state/cache, changing `local-native/**`, deploying, or changing Delete/enhancement isolation contracts.

Tracking:

- GitHub issue: #310
- Pull request: #311
- Local ledger: `jobs.id = 382`
- Baseline: `origin/main` `cf14ae4`
- Branch: `codex/browser-whole-review-20260714`

## Adopted updates

- Global Delete/Favorite/arrow shortcuts now ignore modifier shortcuts, controls, dialogs, and confirmation/settings overlays.
- Modal shortcuts no longer collide with focused toolbar controls; configurable close remains available from a focused control.
- A Favorite changed, decreased, or cleared while the shared snapshot is loading keeps the user's exact current value.
- Old hidden `pvu_view.columns` values migrate to auto layout while other current view settings are retained; non-object snapshots fall back safely.
- Gallery width is remeasured when delayed search results or loading state make the grid appear, preventing 40px-card fallback.
- Empty results distinguish query, client-filter, and folder-empty states.
- Source Delete toolbar and shortcut both honor `Confirm before delete`; Recycle Bin and neighbor continuation are unchanged.
- Dialog roles/names, toolbar accessible names, viewer logo semantics, focus-visible styling, contrast tokens, and bulk-confirm semantics were hardened.
- At 390px the sidebar is an overlay drawer, the gallery uses the full viewport when closed, and all modal toolbar actions remain reachable.
- Add folder / Change folder are visually separated.

## Durable implementation sources

- `docs/browser-feature-contract.md`: browser features, operations, shortcuts, persistence, shared cache, API, Delete, enhancement, and known limitations.
- `docs/browser-to-wpf-parity-plan.md`: browser/WPF matrix, priority, milestones, and acceptance criteria.

Recommended next milestone: `WPF-P0A`, independent exact Favorite Lv1-5 selection with empty-set `All`, persisted `Unseen dots` visibility default OFF, and a Settings foundation. This is intentionally before WPF source Delete.

## Verification

- `scripts/verify-project.ps1`: PASS
- unit: 22 files / 123 tests PASS
- lint: 0 errors; 2 existing `CachedImage` `<img>` warnings
- dependency audit: no known vulnerabilities
- typecheck: PASS
- production build: PASS
- port 3010 production Browser QA: landing, scan, delayed grid layout, search/count, right preview, modal, settings, reload, focused-control Escape, settings shortcut suppression, and 390x844 responsive layout PASS
- console: 0 errors / 0 warnings
- destructive source Delete was not repeated; guarded Recycle Bin/neighbor behavior remains covered by the existing code/tests and previous production verification.

Selected evidence:

- `17-after-desktop-fixed.png`
- `20-after-mobile-modal-final.png`
- `21-after-mobile-sidebar-final.png`
- `22-after-mobile-grid-final.png`
- `23-after-mobile-settings-final.png`

Evidence directory:

`C:\Users\a9ui\.codex\visualizations\2026\07\13\019f5bd3-0f54-7f60-bf8d-ad521fa114c2\browser-whole-review-20260714`

## Known follow-ups

- Full `pvu_view` schema versioning/type-range normalization remains P1; this milestone only recovers the live orphan-column/non-object cases.
- Card/list keyboard collection semantics, dialog focus trap/focus return, and complete WCAG combination measurement remain P2/P1.
- Client-only filter count, preview pin reload semantics, queue pagination, and polling consolidation remain browser follow-ups.
- Browser seen remains `pvu_seen_images`; WPF `.cache/seen.json` is not implicitly synchronized. Any sharing needs an explicit import/export contract.
- WPF source Delete, 1200-item cap removal, and query/date alignment remain separate milestones with acceptance gates in the parity plan.

## Publication and launcher adoption

PR #311 is the publication vehicle. Final merge, normal port 3000 launcher rebuild/restart, GitHub issue closure, and SQLite closeout are recorded in the live GitHub/ledger closeout rather than by deleting or rewriting user state.
