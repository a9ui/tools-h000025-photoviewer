# Operations Log

This file records setup failures, fixes, and repeatable workflow candidates for
H000025. Promote repeated patterns into a Codex skill or System script when the
same manual sequence appears more than twice.

## 2026-06-24 M0 Bootstrap

- Found `node_modules` junctions copied from `H000024_PhotoViewer`; `vitest`,
  `typescript`, and `next` could not resolve because the old target no longer
  existed.
- `pnpm install --frozen-lockfile` prompted about recreating `node_modules` but
  did not repair the links in the non-interactive run.
- `pnpm install --frozen-lockfile --force` recreated local dependencies and
  fixed the junction targets to `H000025_PhotoViewer`.
- `scripts/verify-project.ps1` originally reported `ok: true` even when pnpm
  commands failed; it now throws on non-zero pnpm/corepack exit codes.
- Parent `C:\Users\a9ui\Desktop\Tools` has `.pnpm-store` but no parent
  `node_modules` or parent `pnpm-workspace.yaml`; H000025 is its own pnpm
  workspace.
- GitHub CLI, Cursor CLI, Cursor Agent, Python, Git, and SQLite are available;
  Vercel and Wrangler CLIs are not on PATH.
- ChatGPT Project creation is manual or browser-assisted; record the final URL
  in `project.toml` and sync SQLite after creation.

Skill candidate:

- A reusable `Tools` project M0 bootstrap skill should check copied pnpm
  junctions, repair installs, verify GitHub/SQLite/Cursor/ChatGPT readiness,
  create a private repository, seed milestones/issues, and record service
  checks.
