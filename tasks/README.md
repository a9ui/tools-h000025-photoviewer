# Task Packs

Use one folder per bounded task or GitHub issue.

Recommended files:

- `task.md`: source, goal, acceptance criteria, files to read/touch, forbidden files, verify.
- `plan.md`: short implementation plan and stop conditions.
- `handoff.md`: status, files touched, verify results, decisions, and next action.
- `artifacts/`: small summaries only. Do not store raw logs, local DBs, secrets, or caches.

Create a pack from the Tools root:

```powershell
powershell -ExecutionPolicy Bypass -File .\System\scripts\new-task-pack.ps1 -ProjectRoot .\Projects\<ProjectId_Name> -TaskId <issue-or-task-id> -Title "<short title>"
```
