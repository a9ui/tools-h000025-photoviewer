# Browser Gallery Zoom Anchor - Recap

## Scope

User report: Gallery zoom was again shrinking the left Sidebar and moving the visible area far enough to lose context.

Tracking:

- GitHub issue: #312
- Local ledger: `jobs.id = 384`
- Improvement item: `improvement_items.id = 31`
- Branch: `codex/browser-grid-zoom-anchor-20260714`
- Baseline: `origin/main` `95771386d7e1fb2bcabf819d8a6aa3b08dd586a5`

## Root cause

- This is a regression recovery, not a new product rule. A concrete-image zoom anchor existed in local commit `b584744`, but that mixed/unmerged commit was not part of the current `origin/main`; current main retained only the coarse flat-row correction.
- Modified zoom input could still reach browser page zoom outside Grid mode, scaling Sidebar and all UI text.
- The existing scroll correction reconstructed a center row from flat grid geometry. It did not preserve a concrete image and could not account for date-section headers or per-section row resets.
- At a date header boundary, the 24px Grid top padding was omitted from anchor selection, which could choose the image below the header instead of the closer image above it.

## Adopted behavior

- Ctrl/Cmd-wheel is captured while the viewer is active so browser page zoom does not resize Sidebar or UI text.
- Gallery thumbnail zoom runs only inside the Grid container. List mode blocks page zoom without changing thumbnail size; Alt-only List scrolling remains outside the Gallery zoom handler.
- Ctrl/Cmd `+`, `-`, and `0` keep their Gallery thumbnail semantics while ignoring controls and overlays.
- The zoom snapshot records a concrete image index, its layout top, and its viewport offset. After thumbnail size, column count, or date-section geometry changes, the same image is restored to the same offset before paint.
- Date-section anchor selection uses the image nearest both the vertical viewport center and horizontal content center. The Grid top padding is included when mapping viewport center to layout coordinates.

No Delete, Favorite, Unseen, enhancement enqueue/worker, `local-native/**`, deployment, user cache, or user state semantics were changed.

## Verification

- focused unit tests: 4 files / 30 tests PASS
- targeted ESLint: PASS
- TypeScript: PASS
- production build: PASS
- production Browser QA on port 3011 with 488 images:
  - normal Grid zoom in/out: same image, `0px` top-offset change
  - date-section header boundary zoom in/out: same image, `0px` top-offset change
  - final-row/bottom-edge zoom in/out: same image, `0px` top-offset change and exact round trip
  - Sidebar: `240px` width and `16px` text before/after
  - browser DPR and viewport dimensions unchanged
  - List Ctrl-wheel: thumbnail size, Sidebar, DPR, viewport, and scroll position unchanged
  - reload and re-open of the QA folder set: PASS
  - console errors/warnings: 0

Full project verifier, publication, merge, and normal port 3000 launcher adoption are recorded in the final GitHub/SQLite closeout.
