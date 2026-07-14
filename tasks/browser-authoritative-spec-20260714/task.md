# Browser authoritative specification task

Date: 2026-07-14

## Objective

Audit the Browser PhotoViewer at commit
`626b7dd5416f3619ae59fc66d47e79acd1a74fd5` and the normal production
launcher on port 3000, then publish an implementation-grade specification that
another AI can use to rebuild the product and verify it.

Use that contract to replace stale Browser → WPF assumptions with a live-code
parity plan.

## Deliverables

- `docs/browser-feature-contract.md`
  - requirement IDs
  - UI and state transitions
  - search/filter/sort/Favorite/Seen/zoom/selection/preview/modal
  - Delete and Enhancement safety
  - local APIs and persistence/cache schemas
  - loading/error/empty/responsive/accessibility/performance
  - acceptance tests, limitations, source/test map
- `docs/browser-to-wpf-parity-plan.md`
  - ADOPT / ADAPT / ADD / NATIVE-EXTENSION / DEFER / DROP
  - live WPF evidence
  - P0–P3 implementation order
  - milestone acceptance and risk controls
- `tasks/browser-authoritative-spec-20260714/recap.md`

## Constraints

- No external AI consultation.
- Do not delete or reset user state/cache.
- Do not deploy.
- Do not change Browser, Delete, Enhancement, or `local-native/**` implementation.
- Do not touch unrelated dirty files.
- Use an isolated worktree and documentation-only changes.

## Evidence

- Browser source, routes, tests, CSS, package/launcher at the baseline commit.
- Read-only production interaction at `http://localhost:3000/`.
- Live WPF source under `local-native/PhotoViewer.Wpf/**`.
- GitHub issue #316 and SQLite improvement item #33 / job #386.

## Completion gate

1. Every normative Browser behavior has a stable requirement ID.
2. API request/response shapes match the actual route code.
3. Current limitations are separated from requirements.
4. WPF stale claims are replaced with live-code classifications.
5. Markdown/static checks, project verifier, and read-only Browser console check pass.
6. GitHub and SQLite are updated without deployment or state/cache deletion.
