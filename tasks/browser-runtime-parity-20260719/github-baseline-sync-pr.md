## What

Synchronize the locally adopted PhotoViewer product lineage from GitHub `main@626b7dd` through local `main@3efea54` without adding the 2026-07-19 runtime-parity work in progress.

This draft contains the 153 existing local commits covering the authoritative Browser specification, Browser safety/accessibility/runtime work, and the WPF parity/performance milestones that were developed and verified locally after PR #315.

## Why

GitHub Actions availability and GitHub source/version management are independent. The repository remained usable, but `origin/main` stopped at PR #315 while the normal local launcher advanced by 153 commits. That made branch names and GitHub history an unreliable explanation of the actual local runtime.

This draft restores a reviewable GitHub baseline before the current Browser/WPF runtime-parity fix is published as a small stacked PR.

## Safety

- No force push and no direct update to remote `main`.
- The unrelated local `next-env.d.ts` edit is not committed or included.
- The current 2026-07-19 Browser zoom/Favorite/WPF modal changes are excluded from this baseline branch.
- GitHub Actions is not a merge gate; local verifier evidence will be recorded before this draft is promoted.

## Validation

The individual milestones include their local verifier artifacts and closeout documents. Before merge, the current aggregate Browser unit/typecheck/build/Playwright checks and WPF product/cross-runtime gates will be rerun from the synchronized tip.
