# H000025 Project Router

This project router is copied from the Tools template into H/W projects created
by the Tools system. This project is the active PhotoViewer successor under
`Projects/`; do not route work through archived root-level project folders.

AGENTS.md files are hierarchical. Read the Tools root `AGENTS.md` first, then
this project `AGENTS.md`, then any `AGENTS.md` closer to the target path; the
nearest applicable file controls that area. Use linked Markdown files as the
documented workflow, and do not invent additional workflow rules beyond those
instructions.

After this AGENTS.md, read in order:

1. `PROJECT.md`
2. `DESIGN.md`
3. `project.toml`
4. `START_HERE.md`
5. `docs/operations-log.md`

When this project lives under the Tools folder, the workspace-level operating
rules live in:

1. `..\..\AGENTS.md`
2. `..\..\System\docs\workflow.md`
3. `..\..\System\docs\services.md`
4. `..\..\System\docs\oracle.md`
5. `..\..\System\docs\sqlite-schema.md`
6. `..\..\System\docs\context-budget.md`

Project-local ops examples are copied from the template for humans and agents
when a lane is enabled:

1. `docs\ops\README.md`
2. `docs\ops\agmsg-lanes.md.example`
3. `docs\ops\oracle-lrb-pro.md.example`
4. `docs\ops\cursor-dispatch.md.example`
5. `docs\ops\workspace-hygiene.md.example`
6. `docs\ops\milestone-closeout.md.example`

For existing-project migration, read:

1. `..\..\System\docs\project-migration.md`
2. `tasks\system-migration-20260702-h000025\task.md`
3. `tasks\system-migration-20260702-h000025\plan.md`

Then inspect GitHub milestone, issues, PRs, Actions, SQLite jobs, and local
`git status`.

## Feature Switches

Copy-time defaults are conservative. Project AGENTS may turn a lane ON when the
project needs it and the workspace smoke/preflight passes.

- Agmsg: ON for short pointers, ACKs, and status only.
  Read: `..\..\System\docs\agmsg.md`, `..\..\System\docs\services.md`.
- LRB Oracle: ON after local ON/status preflight; ZIP or sanitized packs are
  fallback.
  Read: `..\..\System\docs\oracle.md`, `..\..\System\docs\services.md`.
- CursorAgent: ON for bounded implementation handoffs through GitHub
  branches/PRs.
  Read: `..\..\System\docs\workflow.md`, `..\..\System\docs\services.md`.
- Claude UI: optional Human Surface review lane after smoke; not a PM.
  Read: `..\..\System\docs\agent-roles.md`, `..\..\System\docs\services.md`.
- GrokSwarmSystem: optional dependency, AGENTS/lower-MD, PR/CI risk, and
  finding lane; not a mass-PR factory.
  Read: `..\..\System\docs\grok-swarm.md`,
  `..\..\System\docs\agent-roles.md`.
- TaskBarQuota WatchDog: OFF by default; enable only when TaskBarQuota writes a
  local snapshot for this project or lane.
  Read: `..\..\System\docs\taskbarquota-watchdog.md`,
  `..\..\System\docs\services.md` before enabling.
- Linear: OFF by default; GitHub remains the implementation source of truth.
  Read: `..\..\System\docs\linear.md`, `..\..\System\docs\services.md`.

Before substantial work, run or confirm the workspace service check from the
Tools root:

```powershell
powershell -ExecutionPolicy Bypass -File ..\..\System\scripts\check-services.ps1
```

If GitHub CLI, Cursor/Composer, pnpm, SQLite, agmsg, GrokCLI, Linear, Chrome /
ChatGPT Project access, or git push fails, record the concrete command and fix
in the project issue/PR and reflect reusable rules back into the Tools system
guidance.

Use Codex for small edits, control work, GitHub/SQLite routing, PR/CI, oracle
packets, and merge decisions. Use Cursor for normal implementation through a
GitHub branch and PR. Use PRO/ChatGPT Project for milestone-level judgment.

Active Codex Goals must be milestone-sized. Do not include endless monitoring,
recurring capture, or delayed-result work in a Goal that cannot close in the
current milestone. At milestone close, write the recap/review packet and hand
off the next milestone to a fresh thread when needed.

## PhotoViewer-Specific Rules

- Preserve the current local-first Next.js PhotoViewer app.
- Do not rename the product as an upscaler.
- Do not remove existing viewer workflows for speed.
- Optional enhancement work starts only from explicit user actions.
- Ordinary browsing, preview, and modal navigation must not enqueue
  enhancement jobs or start workers.
- Linear is not used for this project unless the user explicitly turns it on.
- H projects deploy to Vercel only when deployment is requested and approved.

For complex feature work, check whether GitHub or npm already has a mature,
maintained open-source implementation before building the core logic from
scratch. Reuse it only when license, maintenance, security, size, stack fit, and
integration cost are acceptable; otherwise build the smallest local solution.

For ChatGPT/GPT oracle work, follow the workspace oracle rules: create or reuse
the authenticated Chrome Project named `Codex用（PhotoViewer）`, use only `最高`,
`Pro 拡張`, or `Deep Research` unless explicitly overridden, keep the Project
tab open for handoff, and record a short SQLite `oracle_runs` row plus the
durable GitHub/project-doc outcome.

Follow the workspace context-budget policy before reading large logs, JSONL,
generated reports, raw data, cache folders, local DBs, `.codex`, or `.agents`.
For larger tasks, prefer a task pack under `tasks/` and generate a bounded
context pack instead of re-reading broad project history.
