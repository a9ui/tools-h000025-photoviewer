# Cursor Task: Issue #3 Baseline Instrumentation

Use Composer 2.5 Standard.

Issue: https://github.com/a9ui/tools-h000025-photoviewer/issues/3

## Goal

Add the first repeatable performance baseline harness for PhotoViewer without
changing user-facing viewer behavior.

## Context

- Read `docs/performance/baseline-policy.md`.
- Read `docs/performance/perf-budgets.json`.
- GitHub is the source of truth.
- Keep the task narrow and avoid broad UI refactors.

## Requested Output

- Add package scripts for baseline recording/comparison if needed.
- Add small scripts under `scripts/` that can emit JSON artifacts for at least
  environment metadata, fixture manifest input, and placeholder scenario result
  structure.
- Prefer deterministic, local-only scripts that do not require a real user image
  folder yet.
- Do not implement optimization changes.
- Do not touch enhancement backends except to ensure metrics can represent 0
  enqueue/start counts later.

## Acceptance Criteria

- `pnpm test:unit`, `pnpm typecheck`, and `pnpm build` continue to pass.
- A command like `pnpm perf:record -- --scenario all --output artifacts/perf/current.json`
  creates a structured JSON artifact, even if some metrics are marked
  `pending_fixture`.
- A command like `pnpm perf:compare -- --base artifacts/perf/base.json --candidate artifacts/perf/current.json`
  can compare two artifacts and report pass/fail/pending.
- The artifact includes commit SHA, Node/pnpm versions, platform, timestamp,
  cache state, scenario names, and raw observations.
- Missing real fixture data is represented explicitly, not silently faked.

## Forbidden

- Do not add paid services, API keys, cloud storage, deployment, auth, or
  billing changes.
- Do not remove or rewrite viewer behavior.
- Do not run destructive git commands.
