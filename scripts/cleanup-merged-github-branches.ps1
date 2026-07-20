[CmdletBinding()]
param(
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = 'a9ui/tools-h000025-photoviewer'

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required.'
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required.'
}

$repo = gh repo view $repository --json nameWithOwner,defaultBranchRef | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $repo.nameWithOwner -ne $repository) {
    throw "Refusing to inspect an unexpected repository. Expected $repository."
}
$defaultBranch = [string]$repo.defaultBranchRef.name

$originUrl = [string](git remote get-url origin)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not resolve the origin remote.'
}
$expectedHttpsOrigin = "https://github.com/$repository.git"
$expectedSshOrigin = "git@github.com:$repository.git"
if ($originUrl.TrimEnd('/') -notin @($expectedHttpsOrigin, $expectedHttpsOrigin.Substring(0, $expectedHttpsOrigin.Length - 4), $expectedSshOrigin)) {
    throw "Refusing to combine GitHub data with an unexpected origin remote: $originUrl"
}

$remoteHeads = @{}
foreach ($line in @(git ls-remote --heads origin)) {
    if ($line -notmatch '^([0-9a-f]{40})\s+refs/heads/(.+)$') { continue }
    $remoteHeads[$Matches[2]] = $Matches[1]
}
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read remote branch tips from origin.'
}

$openPullRequestJson = gh pr list --repo $repository --state open --limit 1000 --json number,headRefName,headRefOid,url
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read open pull requests.'
}
$openPullRequests = $openPullRequestJson | ConvertFrom-Json
if ($null -eq $openPullRequests) {
    $openPullRequests = @()
}

$mergedPullRequestJson = gh pr list --repo $repository --state merged --limit 1000 --json number,headRefName,headRefOid,url
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read merged pull requests.'
}
$mergedPullRequests = $mergedPullRequestJson | ConvertFrom-Json
if ($null -eq $mergedPullRequests) {
    $mergedPullRequests = @()
}

$openHeads = @{}
foreach ($pullRequest in $openPullRequests) {
    $openHeads[[string]$pullRequest.headRefName] = $true
}

$eligible = [Collections.Generic.List[object]]::new()
$kept = [Collections.Generic.List[object]]::new()
foreach ($entry in $remoteHeads.GetEnumerator() | Sort-Object Key) {
    $branch = [string]$entry.Key
    $tip = [string]$entry.Value
    if ($branch -eq $defaultBranch) {
        $kept.Add([pscustomobject]@{ Branch = $branch; Tip = $tip; Reason = 'default branch' })
        continue
    }
    if ($openHeads.ContainsKey($branch)) {
        $kept.Add([pscustomobject]@{ Branch = $branch; Tip = $tip; Reason = 'open pull request' })
        continue
    }

    $matchingMerge = @(
        $mergedPullRequests |
            Where-Object {
                [string]$_.headRefName -eq $branch -and
                [string]$_.headRefOid -eq $tip
            } |
            Sort-Object number -Descending |
            Select-Object -First 1
    )
    if ($matchingMerge.Count -ne 1) {
        $kept.Add([pscustomobject]@{ Branch = $branch; Tip = $tip; Reason = 'no merged PR with the exact remote tip' })
        continue
    }

    $eligible.Add([pscustomobject]@{
        Branch = $branch
        Tip = $tip
        PullRequest = [int]$matchingMerge[0].number
        Url = [string]$matchingMerge[0].url
    })
}

Write-Output "Repository: $repository"
Write-Output "Default branch: $defaultBranch"
Write-Output "Remote branches: $($remoteHeads.Count)"
Write-Output "Safe merged-tip deletion candidates: $($eligible.Count)"
Write-Output "Kept branches: $($kept.Count)"

foreach ($candidate in $eligible) {
    Write-Output ("DELETE candidate: {0} @ {1} (merged PR #{2})" -f $candidate.Branch, $candidate.Tip.Substring(0, 8), $candidate.PullRequest)
}
foreach ($branch in $kept) {
    Write-Output ("KEEP: {0} @ {1} ({2})" -f $branch.Branch, $branch.Tip.Substring(0, 8), $branch.Reason)
}

if (-not $Apply) {
    Write-Output 'Dry run only. Re-run with -Apply to delete only the listed merged-tip branches.'
    exit 0
}

foreach ($candidate in $eligible) {
    $encodedBranch = [Uri]::EscapeDataString([string]$candidate.Branch)
    $liveRef = gh api "repos/$repository/git/ref/heads/$encodedBranch" | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw "Could not re-read branch before deletion: $($candidate.Branch)"
    }
    if ([string]$liveRef.object.sha -ne [string]$candidate.Tip) {
        throw "Refusing to delete a branch whose tip changed: $($candidate.Branch)"
    }

    gh api --method DELETE "repos/$repository/git/refs/heads/$encodedBranch" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub refused branch deletion: $($candidate.Branch)"
    }
    Write-Output "Deleted merged branch: $($candidate.Branch)"
}

Write-Output "Deleted $($eligible.Count) merged-tip branch(es). Open, default, unknown, and changed branches were preserved."
