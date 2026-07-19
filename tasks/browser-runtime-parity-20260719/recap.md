# Browser runtime parity and WPF launcher adoption recap — 2026-07-19

Status: implementation and isolated verification are green. Normal-root launcher adoption is the remaining closeout step.

## Runtime diagnosis

- Port 3000 was the normal root at `main@3efea54`, but its build predates this recovery.
- The user-visible WPF process came from `worktrees/wpf-ultimate-0718` and its DLL predates `3efea54`.
- Local branch count was not runtime composition. The exact checked-out build and launcher provenance are the delivery boundary.

## Implemented recovery

### Browser

- Grid owns Ctrl/Cmd+wheel and `+` / `-` / `0` thumbnail zoom while the Grid viewer is active. Sidebar, header, text, and right-panel scale stay unchanged.
- The visible last-selected card is the first zoom anchor; pointer/viewport anchoring is the fallback. Browser page zoom is not used for gallery sizing.
- Visible thumbnail warmup flushes ahead of nearby/background work, retains bounded retry/dedupe state, and gives the newest viewport work priority.
- Favorite/Unrated/Enhanced sparse paging keeps a bounded match demand through sparse pages and reaches catalog tail instead of silently stopping at a partial page.
- Linked worktrees resolve Favorites, Seen, Recent folders, and Enhancement job storage to the normal checkout.
- Browser and WPF share exact Favorite Lv0–5 state. Browser performs a one-time non-destructive local import, refreshes on focus/visibility, and preserves malformed/unknown journal bytes instead of guessing.
- Seen parsing accepts the WPF legacy-compatible stored forms while new Browser writes stay canonical.

### WPF

- App Settings contains discoverable key bindings for the implemented gallery, modal, Favorite, recycle, selection, tab, flip, Enhancement, and zoom actions. Bindings validate conflicts/reserved OS chords, hot-apply, persist, reload in a separate process, and preserve unknown state fields.
- Escape remains a guaranteed rescue from Settings/Delete overlays. Text input, combo/date input, buttons, landing view, and overlays do not leak configured shortcuts or Ctrl/Win+wheel gallery zoom, including WPF template child elements.
- Ctrl+A and Ctrl+Shift+A use the canonical logical selection set rather than materializing 100,000 visual selections. Hidden List/Grid selection cannot reappear as a stale Favorite/Delete target after clear.
- Modal Favorite smoke raises the real `Button.ClickEvent` and verifies UI, disk, and reload.
- PNG catalog metadata now follows first-`parameters`-chunk semantics, including an explicitly empty first chunk.
- 100,000-image startup/background work removes duplicate file probes and PNG reads, unused result dictionaries, and empty-state full-catalog maps while yielding background metadata to visible thumbnail I/O.

## Browser evidence

- Full unit suite: 55 files passed, 2 skipped; 475 tests passed, 2 skipped.
- TypeScript typecheck: PASS.
- ESLint: PASS.
- Next.js optimized production build: PASS.
- Isolated production Playwright on port 43132: 4/4 PASS.
- Selected-card zoom drift: at most 1px in Browser e2e; Sidebar geometry/font/viewport scale unchanged.
- UI retirement/sidebar guard: 18 files PASS.
- E2E routes isolate Recent/legacy/Favorite/Seen state from the real user stores; normal port 3000 is not used by these tests.

## WPF integrated evidence

- Product verifier: 49/49 PASS with catalog stress and reload soak included, 274,307 ms total.
- Release builds in the product verifier: 0 warnings, 0 errors.
- Key-binding verifier: real write/reload processes, template-child input/button wheel isolation, conflict/reserved validation, and stale-hidden-selection suppression PASS.
- Shared-state latency: semantic, actor-adoption, exactness, close/drain, absolute, and strict relative gates PASS. One earlier scheduler outlier was followed by 6/6 focused green runs without weakening the gate.
- Browser/WPF Favorite + Seen contention: 20 iterations, 40 exact entries each, valid JSON, lock/temp residue 0, PASS.
- Browser/WPF/third-writer Recent contention: 20 iterations, unknown fields and latest-owner sets preserved, lock/temp residue 0, PASS.

## Final 100k / 100-folder evidence

The final TEMP-only run loaded exactly 100,000 images from 100 folders with no truncation. Grid/List remained virtualized, tail index 99,999 survived Grid/List round trips and Modal, and all zoom anchors had 0px measured drift.

| Metric | Final frozen-source run |
| --- | ---: |
| Catalog ready | 3,379 ms |
| Full load/background metadata | 28,169 / 24,407 ms |
| Scan/materialize | 1,547 / 1,825 ms |
| Dispatcher max gap | 385 ms |
| Maximum external unresponsive streak | 265 ms (gate: 750 ms) |
| Working set after | 324,218,880 B |
| Grid/List realized | 15 / 9 |
| Tail thumbnail | 172 ms |
| Flat/created zoom drift | 0 / 0 px |

Earlier same-fixture after-runs were 3,762/4,040 ms catalog-ready and 30,756/29,196 ms full-load versus a 5,209/37,192 ms baseline. Managed bytes were not lower, so no managed-heap reduction is claimed.

## Version management

- Remote baseline: `origin/main@626b7dd`.
- Locally adopted baseline: `3efea54`, published without changing remote main as draft PR #319.
- Recovery tracking: GitHub Issue #320 and SQLite improvement item #42 (`in_progress` until normal launcher adoption).
- Recovery branch: `codex/browser-runtime-parity-20260719`; stacked draft PR and normal-root fast-forward follow the frozen commit.

## Safety boundary retained

- No deployment or public release.
- No destructive cache/state deletion or migration reset.
- No change to successful Delete adjacency/navigation semantics or passive Enhancement enqueue behavior.
- All stress/contention fixtures stayed under TEMP and cleaned up.
- The final Playwright pass fingerprints real shared stores before/after and rejects test-history leakage.
- Unrelated normal-root `next-env.d.ts` remains user-owned and untouched.
