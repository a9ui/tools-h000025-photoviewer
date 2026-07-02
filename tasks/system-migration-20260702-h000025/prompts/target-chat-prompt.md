Please migrate this existing Tools project to the current Tools system workflow.

Target:

- Project: H000025 PhotoViewer
- Folder: C:\Users\a9ui\Desktop\Tools\Projects\H000025_PhotoViewer
- Migration pack: tasks/system-migration-20260702-h000025

Read first:

1. ..\..\AGENTS.md
2. AGENTS.md
3. PROJECT.md
4. DESIGN.md
5. project.toml
6. PROJECT_STATE.md
7. ROADMAP.md
8. DECISIONS.md
9. OPEN_QUESTIONS.md
10. CODEX_HANDOFF.md
11. tasks/system-migration-20260702-h000025/task.md
12. tasks/system-migration-20260702-h000025/plan.md

Do:

- Align this project with the current Tools system workflow.
- Do not read all of I000000_CodexSystemCore or all of System/.
- Read only the needed System docs listed in SystemRefs in tasks/system-migration-20260702-h000025/task.md.
- Treat GitHub as the implementation source of truth.
- Treat SQLite as a local ledger/cache only.
- Treat Linear, if enabled, as the top-level progress surface only.
- Treat Agmsg as short pointer/ACK/status transport only.
- Use LRB-first for LRB/PRO oracle work. If LRB is OFF, ask the user to turn it ON first.
- Follow context-budget rules. Do not dump generated reports, full logs, local DBs, .codex, .agents, browser state, local image libraries, generated thumbnails, or enhancement outputs into context.
- Keep PhotoViewer-specific local-first browsing and explicit-enhancement safety rules stronger than generic template wording.
- Do not rename the product as an upscaler, remove existing viewer workflows, start background AI/GPU work automatically, or add cloud/paid APIs.
- Keep changes small and prefer docs/routing/verify surfaces.
- After edits, run: powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
- Record results in tasks/system-migration-20260702-h000025/handoff.md.

First, inspect current state and briefly state the target files and verify commands before editing.
