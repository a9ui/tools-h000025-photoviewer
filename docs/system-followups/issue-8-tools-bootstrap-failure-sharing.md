# Issue #8 Tools Bootstrap Failure Sharing

## Result

Tools-wide bootstrap and service failure sharing was added outside this project
repository because the Tools root is not itself a Git repository.

Updated files:

- `C:\Users\a9ui\Desktop\Tools\AGENTS.md`
- `C:\Users\a9ui\Desktop\Tools\T000000_Template\AGENTS.md`
- `C:\Users\a9ui\Desktop\Tools\System\docs\services.md`
- `C:\Users\a9ui\Desktop\Tools\System\docs\improvement-loop.md`
- `C:\Users\a9ui\Desktop\Tools\System\scripts\check-services.ps1`
- `C:\Users\a9ui\Desktop\Tools\System\scripts\verify-system.ps1`
- `C:\Users\a9ui\Desktop\Tools\System\scripts\integration-smoke.ps1`
- `C:\Users\a9ui\Desktop\Tools\H000025_PhotoViewer\scripts\verify-project.ps1`

## Rules Added

- Bootstrap, dependency, connector, approval, and tool-visibility failures must
  be reflected into Tools-level guidance when they can affect future projects.
- New project templates now tell agents to run the workspace service check
  before substantial work.
- `corepack pnpm` is the preferred verification path when plain `pnpm` points
  at a mismatched global or Codex runtime shim.
- This project's `verify-project.ps1` now prefers `corepack pnpm` before plain
  `pnpm`, so the local verification path follows the same rule.
- GitHub CLI source checks now distinguish a real `gh.exe` from wrappers.
- If `git push` is blocked by local approval policy and approval is unavailable,
  use an approved push later, visible/manual push, or a documented `gh api` REST
  branch/commit/PR fallback.
- Repeated operational sequences become a skill or script when repeated across
  two sessions, when they contain three or more non-obvious commands, or when
  the user asks for future sharing.

## Verification

- `powershell -ExecutionPolicy Bypass -File C:\Users\a9ui\Desktop\Tools\System\scripts\check-services.ps1 -Json`
- `powershell -ExecutionPolicy Bypass -File C:\Users\a9ui\Desktop\Tools\System\scripts\verify-system.ps1 -Json`
- `powershell -ExecutionPolicy Bypass -File C:\Users\a9ui\Desktop\Tools\System\scripts\integration-smoke.ps1 -Json`
- `powershell -ExecutionPolicy Bypass -File C:\Users\a9ui\Desktop\Tools\H000025_PhotoViewer\scripts\verify-project.ps1`

Observed service-check findings:

- `corepack_pnpm`: ok, `corepack pnpm 9.15.9`
- `pnpm_path`: version_mismatch, plain `pnpm` resolves to Codex runtime pnpm
  11.7.0 and should not be used for project verification
- `github_cli_source`: ok, `gh` resolves to the real Tools `gh.exe`
- `git_push_path`: manual_check, REST fallback is documented
- `chrome`: manual, Chrome command is not on PATH but authenticated browser UI
  may still be available

`verify-system.ps1` passed with `failed_count: 0`.

`integration-smoke.ps1` passed with `ok: true` at
`2026-06-27T10:38:31.9374246+09:00`. It verified:

- `sqlite_schema`: ok, using
  `C:\Users\a9ui\Desktop\Tools\System\state\tools.sqlite`
- `github_api_user`: ok, `a9ui`
- `cursor_composer25_roundtrip`: ok, `composer-2.5 direct smoke ok`
- `routing_no_active_i_number`: ok
- `routing_linear_disabled_only`: ok

During this issue, `integration-smoke.ps1` itself was hardened so it no longer
depends on the caller's working directory, uses the real Tools SQLite path, and
caps Composer 2.5 smoke waiting at 45 seconds.

PhotoViewer `verify-project.ps1` passed after switching to `corepack pnpm`:

- unit tests: 72 passed
- typecheck: passed
- production build: passed
- build output: `/` first load JS 133 kB, shared JS 102 kB
