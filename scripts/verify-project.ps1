param(
  [switch]$SkipBuild
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

    $pnpm = Get-Command pnpm -ErrorAction SilentlyContinue
    if ($pnpm) {
      & pnpm @Args
      return
    }

    $corepack = Get-Command corepack -ErrorAction SilentlyContinue
    if (-not $corepack) {
      throw "pnpm is not on PATH and corepack is not available"
    }

    $env:COREPACK_HOME = Join-Path $Root ".cache\corepack"
    New-Item -ItemType Directory -Force $env:COREPACK_HOME | Out-Null
    & corepack pnpm @Args
  }

  $required = @(
    "AGENTS.md",
    "PROJECT.md",
    "DESIGN.md",
    "project.toml",
    "package.json",
    "src\app\page.tsx",
    "docs\requirements.md",
    "docs\spec.md"
  )

  $missing = @($required | Where-Object { -not (Test-Path $_) })
  if ($missing.Count -gt 0) {
    [pscustomobject]@{ ok = $false; missing = $missing } | ConvertTo-Json -Depth 5
    exit 1
  }

  Invoke-Pnpm test:unit
  Invoke-Pnpm typecheck
  if (-not $SkipBuild) {
    Invoke-Pnpm build
  }

  [pscustomobject]@{
    ok = $true
    checks = @("required-files", "pnpm test:unit", "pnpm typecheck", $(if ($SkipBuild) { "build skipped" } else { "pnpm build" }))
  } | ConvertTo-Json -Depth 5
} finally {
  Pop-Location
}
