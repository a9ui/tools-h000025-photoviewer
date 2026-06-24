# Spec

## Architecture

PhotoViewer is a local-first Next.js application. The browser UI talks to local
Next.js route handlers for filesystem browsing, indexing, image serving,
thumbnail work, search, settings, favorites, tags, deletion, and optional
enhancement job control.

## Work Control

- GitHub is the official source for milestones, issues, PRs, Actions, and code.
- SQLite under the Tools `System/state/tools.sqlite` is Codex's local ledger for
  current jobs and service checks.
- Cursor receives normal implementation issues.
- Codex handles small edits, GitHub/SQLite control, PR review, CI triage, and
  oracle packet preparation.
- Linear is not used.

## Measurement Plan

M1 adds or confirms repeatable measurements for:

- startup and first usable screen,
- folder scan progress and completion,
- thumbnail queue and visible viewport fill,
- modal previous/next latency,
- local API response timing,
- enhancement queue isolation.

Measurements should be small and local. They can be implemented as scripts,
unit tests, Playwright checks, or structured runtime logs, as long as each
result is repeatable and recorded in the related issue or PR.

## Optimization Approach

1. Establish a baseline before changing behavior.
2. Pick one bottleneck per issue.
3. Preserve user-visible behavior.
4. Add tests around changed state or routing logic.
5. Compare before/after numbers.
6. Record the validation command and result in the PR.

## Validation

Default local validation:

```powershell
pnpm test:unit
pnpm typecheck
pnpm build
```

Additional checks may include Playwright and targeted performance scripts when
the related issue requires them.
