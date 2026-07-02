# Task: Adopt current Tools system for H25

## Target
- Project: H000025 PhotoViewer
- Path: C:\Users\a9ui\Desktop\Tools\Projects\H000025_PhotoViewer
- Migration pack: tasks/system-migration-20260702-h000025

## Goal

Adopt the current Tools system workflow in this existing project without
restarting the product, rereading the whole system-core project, or overwriting
project-specific safety rules.

## Acceptance Criteria
- Target startup docs route to the current Tools root and System docs.
- GitHub remains the implementation source of truth.
- SQLite remains a local ledger/cache only.
- Linear, if enabled, remains a top-level progress view only.
- Agmsg remains short pointer/ACK/status transport only.
- LRB/PRO oracle work is LRB-first, bounded, and records adoption decisions.
- Context-budget rules are explicit for raw data, generated reports, logs,
  local DBs, .codex, .agents, and archive material.
- Project-specific data and safety rules remain stronger than generic template
  wording.
- Local verify passes.
- Any failed connector/tool/auth path is logged as migration evidence.

## FilesToRead
- AGENTS.md
- PROJECT.md
- DESIGN.md
- project.toml
- PROJECT_STATE.md
- ROADMAP.md
- DECISIONS.md
- OPEN_QUESTIONS.md
- CODEX_HANDOFF.md
- docs/ops/coordination-surfaces.md
- tasks/system-migration-20260702-h000025/plan.md
- tasks/system-migration-20260702-h000025/prompts/target-chat-prompt.md

## SystemRefs
- `..\..\AGENTS.md`
- `..\..\System/docs/workflow.md`
- `..\..\System/docs/services.md`
- `..\..\System/docs/oracle.md`
- `..\..\System/docs/agmsg.md`
- `..\..\System/docs/agent-roles.md`
- `..\..\System/docs/sqlite-schema.md`
- `..\..\System/docs/linear.md`
- `..\..\System/docs/context-budget.md`
- `..\..\System/docs/improvement-loop.md`
- `..\..\System/docs/project-migration.md`
- `..\..\System/scripts/new-task-pack.ps1`
- `..\..\System/scripts/context-pack.ps1`
- `..\..\System/scripts/fail-summary.ps1`
- `..\..\System/scripts/agents-lint.ps1`
- `..\..\System/scripts/scope-guard.ps1`

## FilesToTouch
- AGENTS.md
- PROJECT.md
- DESIGN.md
- project.toml
- PROJECT_STATE.md
- ROADMAP.md
- DECISIONS.md
- OPEN_QUESTIONS.md
- CODEX_HANDOFF.md
- docs/ops/coordination-surfaces.md
- scripts/verify-project.ps1
- tasks/system-migration-20260702-h000025/handoff.md

## NotToTouch
- .env
- .git/
- .codex/
- .agents/
- node_modules/
- tmp/
- .next/
- artifacts/perf/
- local image libraries
- generated thumbnails
- enhancement outputs
- local SQLite DBs
- browser state
- generated reports unless summarized
- archived H000024/H000025 material unless an issue explicitly allowlists it

## Verify
- powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
- powershell -ExecutionPolicy Bypass -File ..\..\System\scripts\check-services.ps1

## No-Change Success

If the target project already satisfies the acceptance criteria, close the task
with a short evidence note and no product-code changes.

## Risks
- Overwriting project-specific safety rules with generic template text.
- Treating Linear, Agmsg, SQLite, or ChatGPT as source of truth.
- Reading too much system-core or old project history into context.
- Mixing migration cleanup with active product work.
