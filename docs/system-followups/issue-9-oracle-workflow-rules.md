# Issue #9 Oracle Workflow Rules

## Result

Tools-wide ChatGPT/GPT oracle workflow rules were updated outside this project
repository because the Tools root is not itself a Git repository.

Updated files:

- `C:\Users\a9ui\Desktop\Tools\AGENTS.md`
- `C:\Users\a9ui\Desktop\Tools\T000000_Template\AGENTS.md`
- `C:\Users\a9ui\Desktop\Tools\System\docs\oracle.md`
- `C:\Users\a9ui\Desktop\Tools\System\docs\sqlite-schema.md`
- `C:\Users\a9ui\Desktop\Tools\System\scripts\verify-system.ps1`

## Rules Added

- ChatGPT Projects are created through authenticated Chrome.
- Project names use `Codex用（<ProjectName>）`, for example
  `Codex用（PhotoViewer）`.
- Project custom instructions are based on stable local project files.
- CAPTCHA, login, and permission prompts stop the run and are reported.
- Created Project tabs stay open for handoff.
- Chrome/oracle subagents must not modify repository files.
- Follow-up PRO review reuses the existing Project tab when available.
- ChatGPT/GPT oracle work defaults only to `最高`, `Pro 拡張`, or
  `Deep Research` unless explicitly overridden.
- PRO uses `Pro 拡張` and waits in roughly 5 minute intervals.
- Oracle completion reports submission success, settings used, answer summary
  or body, blockers, and conversation/project handoff.

## Standard Record

`System/docs/oracle.md` now includes a standard `Oracle Record` format for
GitHub issue comments, milestone notes, or project docs.

`System/docs/sqlite-schema.md` now defines how `oracle_runs` stores only the
compact metadata:

- provider
- mode
- related project / issue
- one-sentence question summary
- short answer summary
- status
- reflected flag
- updated timestamp

Full answers stay in the ChatGPT Project conversation or a sanitized review
pack, not SQLite.

## Verification

- `powershell -ExecutionPolicy Bypass -File C:\Users\a9ui\Desktop\Tools\System\scripts\verify-system.ps1 -Json`
- `powershell -ExecutionPolicy Bypass -File C:\Users\a9ui\Desktop\Tools\System\scripts\check-services.ps1 -Json`

`verify-system.ps1` checks the oracle guidance for Chrome Project use, `最高`,
`Pro 拡張`, `Deep Research`, 5 minute waits, exact project-name format, standard
record format, and the no-repository-write rule for Chrome/oracle subagents.
