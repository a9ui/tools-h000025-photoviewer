[CmdletBinding()]
param(
    [string]$Repository = 'a9ui/H000025-PhotoViewer',
    [string[]]$RequiredCheck = @('Browser verification', 'WPF Release build'),
    [ValidateSet('first_time_contributors_new_to_github', 'first_time_contributors', 'all_external_contributors')]
    [string]$ForkApprovalPolicy = 'first_time_contributors',
    [ValidateRange(1, 400)]
    [int]$ArtifactRetentionDays = 30,
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    throw 'Repository must use owner/name form.'
}
if ($RequiredCheck.Count -eq 0 -or @($RequiredCheck | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
    throw 'At least one non-empty required status check is required.'
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required.'
}

$repo = gh repo view $Repository --json nameWithOwner,visibility,defaultBranchRef | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $repo.nameWithOwner -ne $Repository) {
    throw "Refusing to modify an unexpected repository. Expected $Repository."
}
if ($repo.visibility -ne 'PUBLIC') {
    throw "Refusing to apply public-repository policy to visibility '$($repo.visibility)'."
}
if ($repo.defaultBranchRef.name -ne 'main') {
    throw "Expected default branch 'main', found '$($repo.defaultBranchRef.name)'."
}

$plannedChanges = @(
    'Enable secret scanning and secret-scanning push protection',
    'Enable Dependabot vulnerability alerts without automatic version-update PRs',
    'Enable private vulnerability reporting',
    'Enable CodeQL default setup for JavaScript/TypeScript, C#, and Actions with local-source modeling',
    "Protect main and require checks: $($RequiredCheck -join ', ')",
    'Include administrators, require pull requests and conversation resolution, and block force-push/deletion',
    'Set the default GITHUB_TOKEN permission to read and disallow Actions approval of pull requests',
    "Require maintainer approval for fork workflows according to '$ForkApprovalPolicy'",
    "Retain Actions artifacts and logs for $ArtifactRetentionDays days",
    'Delete merged branches automatically and require full commit SHA references for Actions'
)

Write-Output "Repository: $Repository"
Write-Output 'Planned changes:'
$plannedChanges | ForEach-Object { Write-Output "- $_" }

if (-not $Apply) {
    Write-Output 'Dry run only. Re-run with -Apply after the visibility change and final approval.'
    exit 0
}

function Invoke-GhJson {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)][hashtable]$Body
    )
    $json = $Body | ConvertTo-Json -Depth 10 -Compress
    $json | gh api --method $Method $Endpoint --input - | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub API request failed: $Method $Endpoint"
    }
}

function Invoke-GhEmpty {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Endpoint
    )
    gh api --method $Method $Endpoint | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub API request failed: $Method $Endpoint"
    }
}

Invoke-GhJson -Method PATCH -Endpoint "repos/$Repository" -Body @{
    delete_branch_on_merge = $true
    security_and_analysis = @{
        secret_scanning = @{ status = 'enabled' }
        secret_scanning_push_protection = @{ status = 'enabled' }
    }
}

Invoke-GhEmpty -Method PUT -Endpoint "repos/$Repository/vulnerability-alerts"
Invoke-GhEmpty -Method PUT -Endpoint "repos/$Repository/private-vulnerability-reporting"

Invoke-GhJson -Method PUT -Endpoint "repos/$Repository/branches/main/protection" -Body @{
    required_status_checks = @{
        strict = $true
        contexts = @($RequiredCheck)
    }
    enforce_admins = $true
    required_pull_request_reviews = @{
        dismiss_stale_reviews = $true
        require_code_owner_reviews = $false
        required_approving_review_count = 0
        require_last_push_approval = $false
    }
    restrictions = $null
    required_linear_history = $false
    allow_force_pushes = $false
    allow_deletions = $false
    block_creations = $false
    required_conversation_resolution = $true
    lock_branch = $false
    allow_fork_syncing = $true
}

Invoke-GhJson -Method PUT -Endpoint "repos/$Repository/actions/permissions" -Body @{
    enabled = $true
    allowed_actions = 'all'
    sha_pinning_required = $true
}

Invoke-GhJson -Method PUT -Endpoint "repos/$Repository/actions/permissions/workflow" -Body @{
    default_workflow_permissions = 'read'
    can_approve_pull_request_reviews = $false
}

Invoke-GhJson -Method PUT -Endpoint "repos/$Repository/actions/permissions/fork-pr-contributor-approval" -Body @{
    approval_policy = $ForkApprovalPolicy
}

Invoke-GhJson -Method PUT -Endpoint "repos/$Repository/actions/permissions/artifact-and-log-retention" -Body @{
    days = $ArtifactRetentionDays
}

Invoke-GhJson -Method PATCH -Endpoint "repos/$Repository/code-scanning/default-setup" -Body @{
    state = 'configured'
    query_suite = 'default'
    threat_model = 'remote_and_local'
    languages = @('javascript-typescript', 'csharp', 'actions')
}

Write-Output 'GitHub hardening applied. Verifying current settings...'
gh api "repos/$Repository" --jq '{visibility,delete_branch_on_merge,security_and_analysis}'
if ($LASTEXITCODE -ne 0) { throw 'Repository setting verification failed.' }

gh api "repos/$Repository/branches/main/protection" --jq '{required_status_checks,enforce_admins,required_pull_request_reviews,required_conversation_resolution,allow_force_pushes,allow_deletions}'
if ($LASTEXITCODE -ne 0) { throw 'Branch protection verification failed.' }

gh api "repos/$Repository/actions/permissions"
if ($LASTEXITCODE -ne 0) { throw 'Actions policy verification failed.' }

gh api "repos/$Repository/actions/permissions/workflow"
if ($LASTEXITCODE -ne 0) { throw 'Workflow token policy verification failed.' }

gh api "repos/$Repository/actions/permissions/fork-pr-contributor-approval"
if ($LASTEXITCODE -ne 0) { throw 'Fork workflow approval verification failed.' }

gh api "repos/$Repository/actions/permissions/artifact-and-log-retention"
if ($LASTEXITCODE -ne 0) { throw 'Actions retention verification failed.' }

gh api "repos/$Repository/private-vulnerability-reporting"
if ($LASTEXITCODE -ne 0) { throw 'Private vulnerability reporting verification failed.' }

gh api "repos/$Repository/code-scanning/default-setup"
if ($LASTEXITCODE -ne 0) { throw 'Code scanning default-setup verification failed.' }

Write-Output 'Public repository hardening verified.'
