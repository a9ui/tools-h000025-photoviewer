param(
  [string]$Owner = "a9ui",
  [string]$RepoName = "tools-h000025-photoviewer",
  [string]$MilestoneTitle = "M0"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
chcp 65001 > $null

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$RepoFullName = "$Owner/$RepoName"
$RepoUrl = "https://github.com/$RepoFullName"

Push-Location $Root
try {
  function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    & git -c "safe.directory=$Root" @Args
  }

  if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "gh command is not on PATH"
  }

  & gh auth status | Out-Host
  & gh api user --jq ".login" | Out-Host

  $exists = $true
  & gh repo view $RepoFullName --json url | Out-Null
  if ($LASTEXITCODE -ne 0) { $exists = $false }

  if (-not $exists) {
    & gh repo create $RepoFullName --private
    Invoke-Git remote add origin $RepoUrl
    Invoke-Git push -u origin main
  } else {
    $origin = (& git -c "safe.directory=$Root" remote get-url origin 2>$null)
    if ($LASTEXITCODE -ne 0) {
      Invoke-Git remote add origin $RepoUrl
    } elseif ($origin -ne $RepoUrl -and $origin -ne "$RepoUrl.git") {
      Invoke-Git remote set-url origin $RepoUrl
    }
    Invoke-Git push -u origin main
  }

  $toml = Get-Content -Raw -Encoding UTF8 .\project.toml
  $toml = $toml -replace 'github = ""', ('github = "' + $RepoUrl + '"')
  $toml | Set-Content -Encoding UTF8 .\project.toml

  Invoke-Git diff --quiet -- project.toml
  if ($LASTEXITCODE -ne 0) {
    Invoke-Git add project.toml
    Invoke-Git commit -m "Record GitHub repository URL"
    Invoke-Git push
  }

  $milestones = (& gh api "repos/$RepoFullName/milestones" --jq ".[].title" 2>$null)
  if ($milestones -notcontains $MilestoneTitle) {
    & gh api -X POST "repos/$RepoFullName/milestones" -f "title=$MilestoneTitle" -f "description=Project spine, baseline, and first optimization planning."
  }

  $issues = @(
    @{
      title = "M0: Project spine and baseline"
      body = "Acceptance:`n- GitHub repository exists and is private.`n- project.toml contains the GitHub URL and ChatGPT Project URL.`n- GitHub Actions verify runs on PRs and main.`n- Local verify result is recorded.`n- M1 baseline issues exist."
    },
    @{
      title = "M1: Define repeatable PhotoViewer performance baseline"
      body = "Acceptance:`n- Pick representative local test folders.`n- Measure launch to first usable screen.`n- Measure scan time to first visible result and scan completion.`n- Measure visible thumbnail fill time.`n- Measure modal previous/next latency.`n- Measure local API timings.`n- Record the baseline report in docs/performance/."
    },
    @{
      title = "M1: Prove optional heavy jobs are isolated"
      body = "Acceptance:`n- Opening, previewing, and modal navigation do not start optional heavy jobs.`n- Queue state remains idle during normal browsing.`n- Add or update tests around this isolation if practical."
    },
    @{
      title = "M2: Reduce visible browsing jank"
      body = "Acceptance:`n- Identify redundant renders or state updates around grid, sidebar, preview, and modal.`n- Reduce one bottleneck per PR.`n- Include before/after measurement or profiler notes.`n- Preserve current user-facing behavior."
    },
    @{
      title = "M3: Lighten scan and local API path"
      body = "Acceptance:`n- Identify redundant filesystem or metadata work.`n- Improve cache and invalidation behavior where safe.`n- Keep API compatibility.`n- Include before/after measurement."
    }
  )

  foreach ($issue in $issues) {
    $existing = (& gh issue list --repo $RepoFullName --state all --search $issue.title --json title --jq ".[].title" 2>$null)
    if ($existing -notcontains $issue.title) {
      & gh issue create --repo $RepoFullName --title $issue.title --body $issue.body --milestone $MilestoneTitle
    }
  }

  [pscustomobject]@{
    ok = $true
    repo = $RepoFullName
    url = $RepoUrl
    milestone = $MilestoneTitle
  } | ConvertTo-Json -Depth 5
} finally {
  Pop-Location
}
