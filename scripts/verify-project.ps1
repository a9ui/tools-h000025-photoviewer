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

  function Test-PowerShellSyntax {
    param([string]$RelativePath)
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
      (Join-Path $Root $RelativePath),
      [ref]$tokens,
      [ref]$errors
    ) | Out-Null
    if ($errors.Count -gt 0) {
      $messages = @($errors | ForEach-Object { $_.Message })
      throw "$RelativePath has PowerShell syntax errors: $($messages -join '; ')"
    }
  }

  $required = @(
    "README.md",
    "SECURITY.md",
    "CONTRIBUTING.md",
    "AGENTS.md",
    "PROJECT.md",
    "DESIGN.md",
    "START_HERE.md",
    "project.toml",
    "package.json",
    "src\app\page.tsx",
    "docs\photoviewer-authoritative-spec.md",
    "docs\browser-feature-contract.md",
    "docs\wpf-product-spec.md",
    "docs\public-repository-policy.md",
    "docs\publication-runbook.md",
    "docs\license-decision.md",
    "scripts\verify-public-surface.ps1",
    "scripts\harden-github-repository.ps1",
    ".github\workflows\verify.yml",
    ".github\workflows\security-audit.yml",
    ".github\ISSUE_TEMPLATE\config.yml",
    ".github\ISSUE_TEMPLATE\bug_report.yml",
    ".github\ISSUE_TEMPLATE\feature_request.yml",
    ".github\ISSUE_TEMPLATE\performance_report.yml",
    ".github\pull_request_template.md"
  )

  $missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $Root $_) -PathType Leaf) })
  if ($missing.Count -gt 0) {
    [pscustomobject]@{ ok = $false; missing = $missing } | ConvertTo-Json -Depth 5
    exit 1
  }

  Test-PowerShellSyntax "scripts\verify-public-surface.ps1"
  Test-PowerShellSyntax "scripts\harden-github-repository.ps1"

  $readme = Read-ProjectFile "README.md"
  Test-TextContains "README.md" $readme @(
    "local-first",
    "127.0.0.1",
    "LAN or Internet exposure is unsupported",
    "Windows Recycle Bin",
    "CONTRIBUTING.md",
    "SECURITY.md",
    "H000025-PhotoViewer"
  )

  $security = Read-ProjectFile "SECURITY.md"
  Test-TextContains "SECURITY.md" $security @(
    "local-first",
    "127.0.0.1",
    "private vulnerability reporting",
    "LAN or Internet exposure",
    "personal images",
    "publication-runbook.md"
  )

  $agents = Read-ProjectFile "AGENTS.md"
  Test-TextContains "AGENTS.md" $agents @(
    "self-contained",
    "WinForms is frozen",
    "shell: false",
    "docs/public-repository-policy.md",
    "user-owned port 3000"
  )

  $project = Read-ProjectFile "PROJECT.md"
  Test-TextContains "PROJECT.md" $project @(
    "Public source does not imply deployment",
    "127.0.0.1",
    "Windows Recycle Bin",
    "H000025-PhotoViewer"
  )

  $policy = Read-ProjectFile "docs\public-repository-policy.md"
  Test-TextContains "docs/public-repository-policy.md" $policy @(
    "source repository",
    "application runtime",
    "full-history",
    "final human approval",
    "a9ui/H000025-PhotoViewer"
  )

  $runbook = Read-ProjectFile "docs\publication-runbook.md"
  Test-TextContains "docs/publication-runbook.md" $runbook @(
    "before-public.bundle",
    "gitleaks",
    "trufflehog",
    "gh repo rename",
    "--accept-visibility-change-consequences"
  )

  $package = Read-ProjectFile "package.json" | ConvertFrom-Json
  if ($package.private -ne $true) {
    throw "package.json must remain private to prevent accidental npm publication"
  }
  if ([string]$package.scripts.dev -notmatch "127\.0\.0\.1" -or [string]$package.scripts.start -notmatch "127\.0\.0\.1") {
    throw "package.json dev/start scripts must bind explicitly to 127.0.0.1"
  }

  & (Join-Path $Root "scripts\verify-public-surface.ps1")

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
      "required-public-files",
      "PowerShell syntax",
      "public-surface-policy",
      "pnpm test:unit",
      $(if ($SkipLint) { "lint skipped" } else { "pnpm lint" }),
      $(if ($SkipAudit) { "audit skipped" } else { "pnpm audit --audit-level moderate" }),
      "pnpm typecheck",
      $(if ($SkipBuild) { "build skipped" } else { "pnpm build" }),
      $(if ($Full) { "pnpm test:e2e" } else { "e2e skipped; pass -Full to run" })
    )
  } | ConvertTo-Json -Depth 5
}
finally {
  Pop-Location
}
