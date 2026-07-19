# Browser runtime parity recovery — 2026-07-19

## User-visible regressions

- Browser Grid zoom must resize thumbnails without changing Sidebar/header/text/right-panel scale.
- A visible selected image must remain the zoom anchor so the user does not get lost.
- Visible thumbnails must not wait behind nearby/background warmup work.
- Favorite/Unrated/Enhanced sparse filtered results must continue to the final match instead of appearing capped after one page extension.
- Browser and WPF must use one exact Favorite Lv0–5 history, including WPF decrease/clear.
- WPF modal Favorite `-` / `+` must work through the actual button event and persist.
- WPF must expose discoverable, editable, persistent key settings for commands that actually exist, with duplicate/invalid binding checks and default reset.
- WPF 100,000-image startup, background metadata, memory, scroll, and thumbnail behavior must be measured and improved without truncation.
- Normal launchers, not old linked-worktree binaries or unrelated branch tips, must own the delivered runtime.

## Safety boundary

- Do not delete or rewrite the user's existing cache/state as a test fixture.
- Do not change Delete, passive enhancement behavior, deployment, or local-native WinForms.
- Use TEMP stores and isolated production ports for verification.
- Do not use GitHub Actions as a gate. Fable may perform the user-requested parallel read-only review, but it does not replace local verification.
- Preserve unrelated root change `next-env.d.ts`.

## Runtime diagnosis

- Port 3000 was served from the normal project root at `main@3efea54`, but from a build completed before this recovery.
- The WPF process observed on 2026-07-19 was launched from `worktrees/wpf-ultimate-0718`; its DLL was built at 2026-07-18 13:02 JST, before commit `3efea54` at 13:18 JST.
- Branch count is not runtime composition. Relevant Browser zoom/warmup branches were mostly patch-equivalent to main; selected-image zoom priority was genuinely absent.
- WPF already resolved linked-worktree state to the main checkout, while Browser Favorites defaulted to `process.cwd()`, allowing split histories when a worktree server was launched.
- WPF has functional hard-coded shortcuts but no user-visible binding editor or persistence contract; the absence is now an explicit recovery requirement.
