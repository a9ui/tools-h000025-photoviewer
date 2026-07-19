# H000025 WPF gallery zoom / geometry anchor semantic adoption

Date: 2026-07-19 JST

## Objective

Adopt the WPF gallery zoom / geometry-anchor candidate semantically onto the
shared Search History baseline without regressing Browser/WPF shared state,
async/keyboard/accessibility behavior, or isolated verification.

## Fixed inputs

- Required local-main baseline at branch creation:
  `492988ba2390172c3331b72762f5cecd907a3605`
- Candidate inspected from worktree 3708:
  `b0f9a0e97b7bdbe791d8cd990d1a7973b42bd6e5`
- Dedicated branch:
  `codex/wpf-gallery-zoom-anchor-adoption-20260719`
- Semantic implementation commit:
  `4a22b61`

The candidate was not mechanically cherry-picked. Its WPF zoom, state
migration, virtualization, and geometry-anchor intent was reimplemented on the
required baseline while preserving the baseline's shared Search History code.

## Acceptance contract

- WPF Grid zoom is 20 through 600 in steps of 20; 600 is exactly one column.
- Existing valid state, including 40, remains valid; out-of-range legacy state
  is clamped safely and unknown fields survive.
- Anchor identity is canonical full path, not file name.
- The same path and viewport offset survive zoom, Sidebar, right-panel resize,
  window resize, DPI change, and selected/unselected cases.
- Grid realization remains bounded and List zoom remains rejected without
  breaking recycling virtualization.
- Current Search History async UI, keyboard behavior, accessibility status,
  shared schema protection, and GUID-isolated stall verification remain intact.
- Focused, current aggregate, exact 100,000-image / 100-folder, and shared
  Favorite/Seen/Recent/Search History gates pass.

## Scope boundaries

Do not modify or start the normal Browser root/port 3000, normal WPF runtime,
user state/cache/source, WinForms, deployment, or unrelated dirty files. Do not
push. GitHub Actions and external consultation are not acceptance gates.
