# Browser UI Restore Recap - 2026-07-13

## Outcome

- Restored independent Favorite levels 1-5 with exact-match filtering and `All` semantics.
- Persisted the selected level set, including an explicit empty `All` selection and a legacy single-level fallback.
- Added the persisted `Unseen dots` setting. It controls only the `is-unseen` display class; seen-state meaning and Delete behavior are unchanged.
- Used `b584744` only as a narrow reference. No cherry-pick was performed.

## Verification

- Focused tests: 4 files, 9 tests passed.
- Project verifier: 22 files, 116 tests passed; lint 0 errors with 2 pre-existing `CachedImage.tsx` warnings; audit clean; typecheck passed; production build passed.
- Browser production interaction:
  - Lv1 showed only `02-list.png`.
  - Lv1 + Lv4 showed both fixture images and persisted after reload.
  - `Unseen dots` defaulted OFF, ON added one `is-unseen` card, ON persisted after reload, and OFF removed all `is-unseen` classes.
  - Console errors/warnings: 0.
- Screenshots:
  - `C:\Users\a9ui\.codex\visualizations\2026\07\13\019f5be3-7c3e-72e0-b637-650ba6cf04db\h000025-browser-ui-favorite-levels.png`
  - `C:\Users\a9ui\.codex\visualizations\2026\07\13\019f5be3-7c3e-72e0-b637-650ba6cf04db\h000025-browser-ui-unseen-on.png`

## Boundaries And Publication

- No `local-native/**`, Delete, enhancement enqueue/worker, deployment, or unrelated file changes.
- External consultation was intentionally not used.
- GitHub issue: #308.
- Pull request: #309. GitHub is authoritative for final merge state.
- SQLite carry-forward item: `improvement_items.id = 30`.
