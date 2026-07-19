# Next milestone handoff: adopt WPF gallery zoom / geometry anchor candidate

Create a Goal before work. Start from the then-current local `refs/heads/main`,
never the stale default `origin/main`.

Read in full:

1. `tasks/modal-filmstrip-shared-search-history-20260719/recap.md`
2. `docs/photoviewer-authoritative-spec.md`
3. `docs/current-implementation-truth.md`
4. `docs/wpf-product-spec.md`
5. candidate `b0f9a0e97b7bdbe791d8cd990d1a7973b42bd6e5` and
   `tasks/wpf-gallery-zoom-anchor-20260719/recap.md` in worktree `3708`

Semantic-merge the candidate rather than cherry-picking conflicts blindly.
Preserve the adopted Search History implementation in `App.xaml.cs`,
`MainWindow.xaml`, and `MainWindow.xaml.cs`, plus the GUID-isolated
`verify-wpf-search-stall.ps1` route.

Required outcome:

- WPF Grid range 20..600 with 20px step and exact one-column maximum
- safe migration/clamp of prior state and unknown-field preservation
- canonical path + viewport offset anchor through zoom, Sidebar, right-panel,
  resize, DPI, selected/unselected states
- Grid/List virtualization and List geometry unchanged
- focused zoom/anchor gate, full current aggregate, exact 100,000/100 folders,
  shared Favorite/Seen/Recent/Search History, and normal WPF launcher current

Do not change Browser port 3000, user state/cache/source, WinForms FROZEN,
deployment, or unrelated dirty files. GitHub Actions and external consultation
are not gates. Commit in a dedicated worktree and report adoption evidence
before updating the normal runtime.
