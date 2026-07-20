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

$repo = gh repo view $repository --json nameWithOwner,visibility | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $repo.nameWithOwner -ne $repository) {
    throw "Refusing to modify an unexpected repository. Expected $repository."
}

if ($repo.visibility -ne 'PUBLIC') {
    throw "Refusing to apply the public-repository policy to visibility '$($repo.visibility)'."
}

$plannedChanges = @(
    'Enable secret scanning and secret-scanning push protection',
    'Enable Dependabot vulnerability alerts (without automatic security-update PRs)',
    'Enable private vulnerability reporting',
    'Protect main: require a PR, include administrators, block force-push and deletion, require no Actions check',
    'Delete a merged PR branch automatically',
    'Require full commit SHA references for GitHub Actions'
)

Write-Output "Repository: $repository"
Write-Output 'Planned changes:'
$plannedChanges | ForEach-Object { Write-Output "- $_" }

if (-not $Apply) {
    Write-Output 'Dry run only. Re-run with -Apply to make these GitHub changes.'
    exit 0
}

function Invoke-GhJson {
    param(
        [Parameter(Mandatory)]
        [string]$Method,

        [Parameter(Mandatory)]
        [string]$Endpoint,

        [Parameter(Mandatory)]
        [hashtable]$Body
    )

    $json = $Body | ConvertTo-Json -Depth 10 -Compress
    $json | gh api --method $Method $Endpoint --input - | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub API request failed: $Method $Endpoint"
    }
}

function Invoke-GhEmpty {
    param(
        [Parameter(Mandatory)]
        [string]$Method,

        [Parameter(Mandatory)]
        [string]$Endpoint
    )

    gh api --method $Method $Endpoint | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub API request failed: $Method $Endpoint"
    }
}

Invoke-GhJson -Method PATCH -Endpoint "repos/$repository" -Body @{
    delete_branch_on_merge = $true
    security_and_analysis = @{
        secret_scanning = @{ status = 'enabled' }
        secret_scanning_push_protection = @{ status = 'enabled' }
    }
}

Invoke-GhEmpty -Method PUT -Endpoint "repos/$repository/vulnerability-alerts"
Invoke-GhEmpty -Method PUT -Endpoint "repos/$repository/private-vulnerability-reporting"

Invoke-GhJson -Method PUT -Endpoint "repos/$repository/branches/main/protection" -Body @{
    required_status_checks = $null
    enforce_admins = $true
    required_pull_request_reviews = @{
        dismiss_stale_reviews = $false
        require_code_owner_reviews = $false
        required_approving_review_count = 0
        require_last_push_approval = $false
    }
    restrictions = $null
    required_linear_history = $false
    allow_force_pushes = $false
    allow_deletions = $false
    block_creations = $false
    required_conversation_resolution = $false
    lock_branch = $false
    allow_fork_syncing = $true
}

Invoke-GhJson -Method PUT -Endpoint "repos/$repository/actions/permissions" -Body @{
    enabled = $true
    allowed_actions = 'all'
    sha_pinning_required = $true
}

Write-Output 'GitHub hardening applied. Verifying current settings...'
gh api "repos/$repository" --jq '{visibility,delete_branch_on_merge,security_and_analysis}'
gh api "repos/$repository/branches/main/protection" --jq '{enforce_admins,required_pull_request_reviews,allow_force_pushes,allow_deletions}'
gh api "repos/$repository/actions/permissions"
gh api "repos/$repository/private-vulnerability-reporting"

if ($LASTEXITCODE -ne 0) {
    throw 'GitHub settings were applied, but final verification failed.'
}
