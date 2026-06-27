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
