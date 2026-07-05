param(
  [switch]$SkipBuild,
  [switch]$SkipAudit,
  [switch]$SkipLint,
  [switch]$Full
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
chcp 65001 > $null

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $Root
try {
  function Invoke-Pnpm {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)

    $corepack = Get-Command corepack -ErrorAction SilentlyContinue
    if ($corepack) {
      $env:COREPACK_HOME = Join-Path $Root ".cache\corepack"
      New-Item -ItemType Directory -Force $env:COREPACK_HOME | Out-Null
      & corepack pnpm @Args
      if ($LASTEXITCODE -ne 0) {
        throw "corepack pnpm $($Args -join ' ') failed with exit code $LASTEXITCODE"
      }
      return
    }

    $pnpm = Get-Command pnpm -ErrorAction SilentlyContinue
    if (-not $pnpm) {
      throw "corepack is not available and pnpm is not on PATH"
    }

    & pnpm @Args
    if ($LASTEXITCODE -ne 0) {
      throw "pnpm $($Args -join ' ') failed with exit code $LASTEXITCODE"
    }
  }

  $required = @(
    "AGENTS.md",
    "PROJECT.md",
    "DESIGN.md",
    "project.toml",
    "package.json",
    "src\app\page.tsx",
    "docs\requirements.md",
    "docs\spec.md",
    "tasks\README.md",
    "tasks\system-migration-20260702-h000025\task.md",
    "docs\ops\README.md",
    "docs\ops\agmsg-lanes.md.example",
    "docs\ops\oracle-lrb-pro.md.example",
    "docs\ops\cursor-dispatch.md.example",
    "docs\ops\workspace-hygiene.md.example",
    "docs\ops\milestone-closeout.md.example"
  )

  $missing = @($required | Where-Object { -not (Test-Path $_) })
  if ($missing.Count -gt 0) {
    [pscustomobject]@{ ok = $false; missing = $missing } | ConvertTo-Json -Depth 5
    exit 1
  }

  function Read-ProjectFile {
    param([string]$RelativePath)
    Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $Root $RelativePath)
  }

  function Test-TextContains {
    param(
      [string]$Name,
      [string]$Text,
      [string[]]$Needles
    )
    $missingNeedles = @($Needles | Where-Object { $Text -notmatch [regex]::Escape($_) })
    if ($missingNeedles.Count -gt 0) {
      throw "$Name missing required text: $($missingNeedles -join ', ')"
    }
  }

  $agents = Read-ProjectFile "AGENTS.md"
  Test-TextContains "AGENTS.md" $agents @(
    "Feature Switches",
    "Agmsg: ON",
    "LRB Oracle: ON",
    "CursorAgent: ON",
    "Claude UI: optional",
    "GrokSwarmSystem: optional",
    "TaskBarQuota WatchDog: OFF",
    "Linear: OFF by default",
    "system-migration-20260702-h000025",
    "context-budget"
  )

  $project = Read-ProjectFile "PROJECT.md"
  Test-TextContains "PROJECT.md" $project @(
    "GitHub is the official source of truth",
    "SQLite is a local ledger",
    "Large artifacts follow"
  )

  $design = Read-ProjectFile "DESIGN.md"
  Test-TextContains "DESIGN.md" $design @(
    "Human Surface",
    "Data / State",
    "Error / Empty / Warning States"
  )

  $opsReadme = Read-ProjectFile "docs\ops\README.md"
  Test-TextContains "docs/ops/README.md" $opsReadme @(
    "Ops Examples",
    "agmsg.md",
    "oracle.md",
    "workflow.md"
  )

  $opsAgmsg = Read-ProjectFile "docs\ops\agmsg-lanes.md.example"
  Test-TextContains "docs/ops/agmsg-lanes.md.example" $opsAgmsg @(
    "Reply mechanics are lane-specific",
    "Grok live calls are opt-in",
    "agmsg-roundtrip-smoke.ps1",
    "-Lane cursor,claude"
  )

  Invoke-Pnpm test:unit
  if (-not $SkipLint) {
    Invoke-Pnpm lint
  }
  if (-not $SkipAudit) {
    Invoke-Pnpm audit --audit-level moderate
  }
  Invoke-Pnpm typecheck
  if (-not $SkipBuild) {
    Invoke-Pnpm build
  }
  if ($Full) {
    Invoke-Pnpm test:e2e
  }

  [pscustomobject]@{
    ok = $true
    checks = @(
      "required-files",
      "pnpm test:unit",
      $(if ($SkipLint) { "lint skipped" } else { "pnpm lint" }),
      $(if ($SkipAudit) { "audit skipped" } else { "pnpm audit --audit-level moderate" }),
      "pnpm typecheck",
      $(if ($SkipBuild) { "build skipped" } else { "pnpm build" }),
      $(if ($Full) { "pnpm test:e2e" } else { "e2e skipped; pass -Full to run" })
    )
  } | ConvertTo-Json -Depth 5
} finally {
  Pop-Location
}
