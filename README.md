# PhotoViewer

PhotoViewer is a local-first Windows-oriented image viewer. This H24 reboot
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

- `docs/requirements.md`: what this H24 reboot must preserve and improve.
- `docs/spec.md`: how measurement and optimization work should proceed.
- `docs/legacy/`: copied reference material from the previous H24 workspace.
