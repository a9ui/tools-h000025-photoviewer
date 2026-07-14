# Browser Sidebar cleanup recap

## Scope

- Removed the Sidebar `Quick Search` preset section.
- Removed the `Today`, `7d`, `30d`, `This year`, and preset `Clear` row.
- Preserved manual `Date from` / `Date to` inputs and the active `Clear date filter` action.
- Made `Folders` default-expanded and collapsible from its heading button with
  `aria-expanded` and `aria-controls`.
- Updated the current Browser contract and Browser-to-WPF parity plan so removed
  presets are not treated as required WPF parity.

## No-touch boundaries

- No user state or cache was deleted.
- Delete, enhancement enqueue/worker, local-native, and deployment were not changed.
- The unrelated root `next-env.d.ts` modification was preserved.

## Verification

- Focused Sidebar tests: 3/3 passed.
- Full project verifier: 22 test files / 128 unit tests, 2 Playwright E2E tests,
  lint 0 errors (2 pre-existing `no-img-element` warnings), audit clean,
  typecheck and production build passed.
- Production Browser at `http://127.0.0.1:3011/`, 1280x720:
  - page title `PhotoViewer`, meaningful viewer DOM, and no framework overlay;
  - `Quick Search`, `Today`, `7d`, `30d`, and `This year` counts were all zero;
  - manual date inputs remained visible;
  - `Folders` changed `true -> false -> true`; child controls disappeared while
    collapsed and returned after expansion;
  - reload returned the same default-expanded contract;
  - console errors/warnings: 0.

## Tracking

- GitHub issue: #314
- SQLite `improvement_items`: #32
- SQLite `jobs`: #385
- Branch: `codex/browser-sidebar-cleanup-20260714`

PR merge and normal port 3000 launcher adoption are recorded in the issue and
SQLite closeout after publication.
