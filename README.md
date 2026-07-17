# PhotoViewer

PhotoViewer is a local-first Windows-oriented image viewer. This H25 project
keeps the existing viewer functions and focuses the project on making browsing,
scanning, thumbnailing, search, modal navigation, and local API behavior as
light as possible.

## Goal

Keep the current features, then improve the experience with repeatable
measurements:

- launch to first usable screen,
- scan progress and scan completion,
- visible thumbnail fill,
- modal previous/next latency,
- local API response time,
- proof that optional heavy jobs never start without explicit user action.

## Workflow

- `PROJECT.md` owns the big goal and roadmap.
- GitHub owns milestones, issues, PRs, Actions, and code history.
- SQLite under the Tools `System` folder is Codex's local job ledger.
- Cursor handles normal implementation issues.
- Codex handles small edits, GitHub/SQLite control, PR review, CI, and oracle
  packet preparation.
- Linear is not used.

## Commands

```powershell
pnpm install --frozen-lockfile
pnpm dev
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
```

Default verification runs:

```powershell
pnpm test:unit
pnpm typecheck
pnpm build
```

## Docs

- `docs/ai-implementation-brief.md`: exact read order and non-negotiable build/verification handoff for another AI or team.
- `docs/browser-feature-contract.md`: normative, implementation-ready Browser product specification.
- `docs/wpf-product-spec.md`: normative WPF product/state/safety/acceptance specification.
- `docs/browser-to-wpf-parity-plan.md`: initial gap history, live completion ledger, and remaining slices.
- `docs/product-review-20260718.md`: current cross-surface review, risk closure, quality assessment, and priorities.
- `docs/requirements.md`: what this H25 project must preserve and improve.
- `docs/spec.md`: how measurement and optimization work should proceed.
- `docs/legacy/`: copied reference material from the previous workspace.

WPF focused verification starts with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-ui-regression-guard.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-p0.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-p1a.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-p1b.ps1
```
