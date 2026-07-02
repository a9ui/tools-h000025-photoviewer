# Plan: Adopt current Tools system for H25

## Current Evidence

- This is an existing project. Start from its local router and current GitHub
  state, not from the system-core conversation.
- Read 	asks/system-migration-20260702-h000025/task.md before editing.
- Read ..\..\System\docs\project-migration.md for the migration contract.

## Proposed Steps

1. Confirm current target state from project docs, GitHub, SQLite, and local
   git status.
2. Compare target routing against current Tools root/System docs.
3. Patch only drift that blocks the current workflow:
   - startup order,
   - source-of-truth wording,
   - Linear/SQLite/Agmsg role boundaries,
   - LRB/PRO oracle route,
   - context-budget and artifact policy,
   - verify/service check references.
4. Run the project verify command.
5. Record results in handoff.md, GitHub issue/PR, and SQLite if this becomes
   active implementation work.

## Stop Conditions

- Work needs secrets, browser cookies, private paid-account automation, billing,
  or production deploy approval.
- The migration requires raw data, local DB dumps, full old reports, or archived
  agent state in model context.
- The task starts changing prediction logic, data imports, UI behavior, or
  canary settlement rules outside a scoped GitHub issue.
- LRB/PRO review is requested but LRB is OFF; ask the user to turn it ON first.
