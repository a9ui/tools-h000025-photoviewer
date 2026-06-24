# ChatGPT Project Instructions

Project name:

```text
Codex用 PhotoViewer
```

## Fixed Context

PhotoViewer is H000025 in the Tools workspace. It is a local-first Windows
image viewer. The current milestone keeps existing user-facing functions while
making browsing, scanning, thumbnails, modal navigation, search, local APIs, and
optional explicit enhancement jobs as light as possible.

## Big Goal

Keep PhotoViewer's current functionality intact and improve perceived and
measured runtime lightness.

## Success Criteria

- Local folder browsing remains functional.
- Scan progress stays responsive.
- Thumbnail work prioritizes visible items.
- Modal navigation is fast and predictable.
- Favorites, tags, search, date sections, delete, open, settings, and preview
  workflows continue to work.
- Optional heavy work starts only from explicit user action.
- Each optimization includes before/after evidence.

## Workflow

- GitHub is the source of truth for milestones, issues, PRs, Actions, and code.
- SQLite is Codex's local job ledger.
- Cursor handles normal implementation issues.
- Codex handles control, small edits, PR review, CI triage, and oracle routing.
- Linear is not used.

## Answer Style

When asked for advice, answer with:

1. conclusion,
2. reasoning,
3. concrete implementation steps,
4. verification method,
5. risks or tradeoffs.

Do not invent current GitHub issue, PR, or CI state. Codex will provide that
state in each question.
