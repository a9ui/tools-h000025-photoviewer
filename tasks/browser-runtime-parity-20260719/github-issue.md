## Problem

The normal local Browser/WPF product lineage advanced beyond GitHub `main`, while runtime adoption was not consistently tied to the normal project launchers. The observed WPF process came from a linked worktree binary built before its checkout tip. Browser Grid native page zoom could resize the Sidebar, selected cards were not the first zoom anchor, visible thumbnail warmup waited behind delayed work, Favorite-only sparse loading could appear to stop, and Browser/WPF exact Favorite decreases/clears were not live-reconciled.

## Scope

- Keep Grid zoom inside the gallery and preserve fixed Sidebar/header/text/right-panel scale.
- Prefer the visible last-selected card as zoom anchor for slider, Ctrl/Cmd wheel, and `+` / `-` / `0`.
- Dispatch visible thumbnail warmup immediately with bounded retry and no large JS blob cache.
- Make Favorite-only/Unrated/Enhanced sparse results reach the complete matching set without a silent cap.
- Resolve Browser shared Favorites to the normal checkout even from a linked worktree.
- Treat shared Favorite Lv0–5 as exact after one-time non-destructive local-only migration; refresh on focus/visibility.
- Exercise WPF modal Favorite through the actual Button click event and exact shared persistence.
- Add discoverable WPF key settings for implemented commands, including edit/save/reload persistence/default reset and duplicate or invalid binding rejection.
- Re-measure WPF 100,000-image startup/scroll/thumbnail/memory responsiveness and adopt only a verified normal-root build.

## Safety

- Preserve user state/cache and unrelated dirty files.
- TEMP fixtures and isolated Browser ports only until final launcher adoption.
- No deployment, no Delete semantics changes, and no passive enhancement work.
- GitHub Actions is not a gate; local unit/typecheck/build/Playwright/WPF/cross-runtime evidence is required.

## Version management

- Baseline sync is tracked separately in draft PR #319.
- The focused fix will be a stacked PR on the baseline branch, then retargeted to `main` after the baseline is adopted.
