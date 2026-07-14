# Browser Whole Review - Task

## Goal

現行 browser production surface をコード、runtime、主要操作、state、accessibility、responsive、performance、Delete/Favorite/Seen/Enhancement の契約までレビューし、安全で再現可能な改善を反映する。現状機能を WPF 等の implementation source に使える詳細文書として固定する。

## Authority

- GitHub issue: #310
- Branch: `codex/browser-whole-review-20260714`
- Baseline: `origin/main` `cf14ae4`
- Local ledger job: `jobs.id = 382`

## Boundaries

- existing user state/cache を削除しない。
- deployment しない。
- `local-native/**` を変更しない。
- enhancement enqueue/worker isolation と Delete Recycle Bin contract を壊さない。
- external consultation を使わない。
- root checkout の unrelated `next-env.d.ts` を変更しない。

## Deliverables

- safe browser hardening and regression tests
- `docs/browser-feature-contract.md`
- `docs/browser-to-wpf-parity-plan.md`
- production build + real browser before/after evidence
- GitHub / SQLite closeout and next WPF milestone handoff
