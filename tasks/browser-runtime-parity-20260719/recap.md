# Browser runtime parity and WPF launcher adoption recap — 2026-07-19

## 2026-07-19 20:56 JST follow-up adoption

Local `main` was fast-forwarded to `53dc865c08e5009b511e39f0f23405e15730b3db`
without changing the pre-existing user-owned `next-env.d.ts` SHA-256
`7B550DDA9686C16F36A17BF9051D5DBF31E98555B30D114AC49FC49A1E712651`.
This follow-up adds and adopts the following verified recovery:

- Browser thumbnail sizing now spans 20px through an exact one-column endpoint.
  The visible selected card remains the primary anchor across thumbnail zoom and
  Sidebar collapse/expand; the fixed Sidebar itself does not scale.
- An expired in-memory `indexToken` now returns an explicit HTTP 410. Cached
  images stop retrying the expired session, the modal retains its thumbnail
  preview, and the current folder set is automatically rescanned once while
  search/filter/viewer state remains visible.
- Browser and WPF require an explicit confirmation whenever any Delete target
  is a Favorite, even when the ordinary confirmation preference is disabled.
  The destructive execution boundary independently rejects an unconfirmed
  Favorite target. Source deletion remains Windows Recycle Bin-only.
- Windows `EBUSY`/`EPERM`/`EACCES` while publishing an Enhancement output now
  uses bounded rename retries followed by an awaited safe-copy fallback. The
  job is not marked successful before the final output is durable.

Verification at `53dc865`:

- Browser unit: 59 files passed, 2 skipped; 500 tests passed, 2 skipped.
- Browser optimized build, TypeScript, full ESLint, and `git diff --check`: PASS.
- Isolated Browser Playwright zoom/modal contract: 3/3 PASS.
- WPF aggregate: 48/48 PASS in 268,242ms (`-SkipStress`); exact 100,000 images
  / 100 folders ran separately and PASSed with catalog/filtered/source all
  100,000, silent truncate 0, Grid/List realized 15/9, tail index 99,999,
  warm metadata 213ms, warm full load 3,997ms, zoom drift 0px, and Enhancement
  reads/candidates 0. TEMP cleanup succeeded.
- A quiet three-run 100,000-entry shared Favorite/Seen latency gate PASSed:
  Modal p95 4.382/5.743/6.840ms, Favorite p95 29.744/38.866/25.217ms, and
  dispatcher max gap 57.049/47.129/48.639ms.
- Normal `start_viewer.bat` rebuilt and serves revision `53dc865` on loopback
  `127.0.0.1:3000`; `verify-browser-runtime.ps1` PASSed.
- Normal `start_wpf.bat` correctly detected the stale Release target. After the
  existing normal-root WPF window closed through its normal window-close path,
  it rebuilt with 0 warnings/errors, recorded provenance, completed the real
  launcher startup smoke in 517ms, and the relaunched normal-root WPF process
  was responding with a visible `PhotoViewer` window.
- Revision `53dc865` was pushed to
  `origin/codex/wpf-thumbnail-performance-20260719`; Draft PR #322 now preserves
  and exposes the full local-main recovery against the older default
  `origin/main`. GitHub Actions remains outside the gate.

Status: complete locally at implementation revision `53dc865`. Both normal
launchers were verified at that implementation revision. Subsequent commits in
this closeout are documentation-only and do not change the runtime source.

## Runtime diagnosis

- Port 3000 was the normal root at `main@3efea54`, but its build predated this recovery.
- The initially visible WPF process came from `worktrees/wpf-ultimate-0718` and its DLL predated `3efea54`.
- Local branch count was not runtime composition. The checked-out revision, build, executable path, and launcher provenance are the delivery boundary.

## Implemented recovery

### Browser

- Grid owns Ctrl/Cmd+wheel and `+` / `-` / `0` thumbnail zoom while the Grid viewer is active. Sidebar, header, text, and right-panel scale stay unchanged.
- The visible last-selected card is the first zoom anchor; pointer/viewport anchoring is the fallback. Browser page zoom is not used for gallery sizing.
- Visible thumbnail warmup flushes ahead of nearby/background work, retains bounded retry/dedupe state, and gives the newest viewport work priority.
- Favorite/Unrated/Enhanced sparse paging keeps bounded match demand through sparse pages and reaches catalog tail instead of silently stopping at a partial page.
- Linked worktrees resolve Favorites, Seen, Recent folders, and Enhancement job storage to the normal checkout.
- Shared cache resolution uses a closed set of literal store paths so production builds do not glob the real thumbnail cache.
- Browser and WPF share exact Favorite Lv0–5 state. Browser performs a one-time non-destructive local import, refreshes on focus/visibility, and preserves malformed/unknown journal bytes instead of guessing.
- Seen parsing accepts WPF-compatible legacy stored forms while new Browser writes stay canonical.

### WPF

- App Settings contains discoverable key bindings for the implemented gallery, modal, Favorite, recycle, selection, tab, flip, Enhancement, and zoom actions. Bindings validate conflicts/reserved OS chords, hot-apply, persist, reload in a separate process, and preserve unknown state fields.
- Escape remains a guaranteed rescue from Settings/Delete overlays. Text input, combo/date input, buttons, landing view, and overlays do not leak configured shortcuts or Ctrl/Win+wheel gallery zoom, including WPF template child elements.
- Ctrl+A and Ctrl+Shift+A use the canonical logical selection set instead of materializing 100,000 visual selections. Hidden List/Grid selection cannot reappear as a stale Favorite/Delete target after clear.
- Modal Favorite smoke raises the real `Button.ClickEvent` and verifies UI, disk, and reload.
- PNG catalog metadata follows first-`parameters`-chunk semantics, including an explicitly empty first chunk.
- 100,000-image startup/background work removes duplicate file probes and PNG reads, unused result dictionaries, and empty-state full-catalog maps while yielding background metadata to visible thumbnail I/O.

## Browser evidence

- Full unit suite: 55 files passed, 2 skipped; 475 tests passed, 2 skipped.
- TypeScript typecheck, ESLint, UI regression guard, and optimized production build: PASS.
- Isolated production Playwright on port 43132: 4/4 PASS with `sourceDirty=false`.
- Normal `start_viewer.bat` rebuilt port 3000 at implementation revision `c8dfc3d`, with no Turbopack warning and loopback-only runtime verification PASS.
- Normal port 3000 Playwright: 4/4 PASS. Selected-card zoom drift was at most 1px and Sidebar geometry/font/viewport scale stayed unchanged.
- E2E routes isolate Recent/legacy/Favorite/Seen state. SHA-256, length, and mtime for Favorites, Seen, Recent, Enhancement jobs, and WPF state were identical before/after both final Playwright passes.
- Runtime `sourceDirty=true` on normal root is explained solely by the pre-existing user-owned `next-env.d.ts`; its hash was preserved through both fast-forwards.

## WPF integrated and launcher evidence

- Product verifier: 49/49 PASS with catalog stress and reload soak included, 274,307 ms total.
- Release builds in the product verifier: 0 warnings, 0 errors.
- Key-binding verifier: real write/reload processes, template-child input/button wheel isolation, conflict/reserved validation, and stale-hidden-selection suppression PASS.
- Shared-state latency: semantic, actor-adoption, exactness, close/drain, absolute, and strict relative gates PASS. One earlier scheduler outlier was followed by 6/6 focused green runs without weakening the gate.
- Browser/WPF Favorite + Seen contention: 20 iterations, 40 exact entries each, valid JSON, lock/temp residue 0, PASS.
- Browser/WPF/third-writer Recent contention: 20 iterations, unknown fields and latest-owner sets preserved, lock/temp residue 0, PASS.
- Normal `start_wpf.bat` detected the old source revision, rebuilt with 0 warnings/errors, recorded provenance, and launched the normal-root Release executable. The freshness checker returned `current / provenance-match`; the process was responding with a visible `PhotoViewer` window.

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

- Remote baseline remains `origin/main@626b7dd`; draft baseline PR #319 points to `3efea54`.
- Implementation commit: `c8dfc3d` on `codex/browser-runtime-parity-20260719`; this recap is a following docs-only commit.
- Recovery tracking: GitHub Issue #320 and SQLite improvement item #42.
- `git push -u origin codex/browser-runtime-parity-20260719` was rejected by the current Codex external-write approval policy before execution. The local branch and normal-root `main` retain the complete commits; GitHub Actions was not used as a gate.

## Safety boundary retained

- No deployment or public release.
- No destructive cache/state deletion or migration reset.
- No change to successful Delete adjacency/navigation semantics or passive Enhancement enqueue behavior.
- All stress/contention fixtures stayed under TEMP and cleaned up.
- Two exact `photoviewer-grid-zoom-e2e-*` Recent entries created by an earlier pre-isolation pass were detected, removed, and the prior three-folder last set restored. No other Recent entry or shared store was removed.
- Unrelated normal-root `next-env.d.ts` remains user-owned and hash-identical to its pre-adoption state.
