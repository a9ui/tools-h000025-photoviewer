[CmdletBinding()]
param(
    [switch]$FullTree,
    [switch]$RequireLicense,
    [string]$ExpectedRepository = '',
    [string]$OutputPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $Root
try {
    function Add-Finding {
        param(
            [string]$Rule,
            [string]$Path,
            [int]$Line,
            [string]$Message
        )
        $script:Findings.Add([pscustomobject]@{
            rule = $Rule
            path = $Path
            line = $Line
            message = $Message
        }) | Out-Null
    }

    function Add-Warning {
        param([string]$Rule, [string]$Message)
        $script:Warnings.Add([pscustomobject]@{
            rule = $Rule
            message = $Message
        }) | Out-Null
    }

    function Is-TextCandidate {
        param([string]$Path)
        $extension = [IO.Path]::GetExtension($Path).ToLowerInvariant()
        return $extension -notin @(
            '.png', '.jpg', '.jpeg', '.gif', '.webp', '.avif', '.bmp', '.ico',
            '.pdf', '.zip', '.7z', '.gz', '.bin', '.exe', '.dll', '.pdb',
            '.woff', '.woff2', '.ttf', '.otf', '.pvmi'
        )
    }

    function Test-FilePatterns {
        param([string]$RelativePath, [hashtable]$Patterns)
        $absolutePath = Join-Path $Root $RelativePath
        if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) { return }
        if (-not (Is-TextCandidate $RelativePath)) { return }
        if ($RelativePath -eq 'scripts/verify-public-surface.ps1') { return }

        $lineNumber = 0
        try {
            Get-Content -LiteralPath $absolutePath -Encoding UTF8 | ForEach-Object {
                $lineNumber++
                $line = [string]$_
                foreach ($entry in $Patterns.GetEnumerator()) {
                    if ($line -match $entry.Value) {
                        Add-Finding -Rule $entry.Key -Path $RelativePath -Line $lineNumber `
                            -Message 'Potential secret, personal path, or private workspace reference. Review the source line locally; matched content is intentionally not echoed.'
                    }
                }
            }
        }
        catch {
            Add-Warning -Rule 'unreadable-text' -Message "Could not inspect $RelativePath as UTF-8 text: $($_.Exception.Message)"
        }
    }

    function Test-WorkflowPolicy {
        param([string]$RelativePath)
        $workflow = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $Root $RelativePath)

        if ($workflow -notmatch '(?ms)^permissions:\s*\r?\n\s+contents:\s*read\s*$') {
            Add-Finding -Rule 'workflow-permissions' -Path $RelativePath -Line 1 `
                -Message 'Each workflow must declare top-level contents: read permissions.'
        }
        if ($workflow -match '(?m)^\s*pull_request_target\s*:') {
            Add-Finding -Rule 'pull-request-target' -Path $RelativePath -Line 1 `
                -Message 'Public fork verification must not use pull_request_target.'
        }

        $usesLines = @($workflow -split "`r?`n" | Where-Object { $_ -match '^\s*-?\s*uses:\s*([^\s#]+)' })
        foreach ($line in $usesLines) {
            $null = $line -match '^\s*-?\s*uses:\s*([^\s#]+)'
            $reference = $Matches[1]
            if ($reference -notmatch '@[0-9a-fA-F]{40}$') {
                Add-Finding -Rule 'unpinned-action' -Path $RelativePath -Line 1 `
                    -Message "GitHub Action is not pinned to a full commit SHA: $reference"
            }
        }

        if ($workflow -match 'actions/checkout@' -and
            $workflow -notmatch '(?ms)uses:\s*actions/checkout@[0-9a-fA-F]{40}.*?persist-credentials:\s*false') {
            Add-Finding -Rule 'checkout-credentials' -Path $RelativePath -Line 1 `
                -Message 'actions/checkout must disable persisted credentials.'
        }
    }

    $Findings = [System.Collections.Generic.List[object]]::new()
    $Warnings = [System.Collections.Generic.List[object]]::new()

    $tracked = @(& git ls-files)
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }

    $publicFiles = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    @(
        'README.md',
        'SECURITY.md',
        'CONTRIBUTING.md',
        'AGENTS.md',
        'PROJECT.md',
        'DESIGN.md',
        'START_HERE.md',
        'project.toml',
        'docs/public-repository-policy.md',
        'docs/publication-runbook.md',
        'docs/license-decision.md',
        'scripts/harden-github-repository.ps1'
    ) | ForEach-Object { [void]$publicFiles.Add($_) }

    $tracked | Where-Object { $_ -like '.github/*' } | ForEach-Object {
        [void]$publicFiles.Add($_)
    }

    $pathsToInspect = if ($FullTree) {
        $tracked
    }
    else {
        @($publicFiles | Where-Object { $tracked -contains $_ })
    }

    $patterns = [ordered]@{
        'windows-user-home' = '(?i)[A-Z]:\\Users\\[^\\\r\n]+'
        'mac-user-home' = '(?i)(^|[/\\])Users[/\\][^/\\\s]+'
        'linux-user-home' = '(?i)(^|[/\\])home[/\\][^/\\\s]+'
        'private-tools-root' = '(?i)Desktop[/\\]+Tools'
        'pem-private-key' = '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
        'github-token' = '(?i)(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]+'
        'aws-access-key' = '(?<![A-Z0-9])AKIA[0-9A-Z]{16}(?![A-Z0-9])'
        'openai-key' = '(?i)(?<![A-Za-z0-9])sk-(?:proj-)?[A-Za-z0-9_-]{20,}'
    }

    foreach ($relativePath in $pathsToInspect) {
        Test-FilePatterns -RelativePath $relativePath -Patterns $patterns
    }

    $workflowFiles = @($tracked | Where-Object { $_ -match '^\.github/workflows/[^/]+\.ya?ml$' })
    if ($workflowFiles.Count -eq 0) {
        Add-Finding -Rule 'missing-workflow' -Path '.github/workflows' -Line 0 `
            -Message 'At least one GitHub Actions workflow is required.'
    }
    foreach ($workflowPath in $workflowFiles) {
        Test-WorkflowPolicy -RelativePath $workflowPath
    }

    $package = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $Root 'package.json') | ConvertFrom-Json
    foreach ($scriptName in @('dev', 'start')) {
        $command = [string]$package.scripts.$scriptName
        if ($command -notmatch '127\.0\.0\.1') {
            Add-Finding -Rule 'loopback-script' -Path 'package.json' -Line 1 `
                -Message "package.json script '$scriptName' must bind explicitly to 127.0.0.1."
        }
    }
    if ($package.private -ne $true) {
        Add-Finding -Rule 'npm-publication' -Path 'package.json' -Line 1 `
            -Message 'package.json must remain private to prevent accidental npm publication.'
    }

    if ($RequireLicense -and -not (Test-Path -LiteralPath (Join-Path $Root 'LICENSE') -PathType Leaf)) {
        Add-Finding -Rule 'missing-license' -Path 'LICENSE' -Line 0 `
            -Message 'A root LICENSE file is required for the publication gate.'
    }
    elseif (-not (Test-Path -LiteralPath (Join-Path $Root 'LICENSE') -PathType Leaf)) {
        Add-Warning -Rule 'missing-license' -Message 'No root LICENSE exists; repository publication remains blocked.'
    }

    if ($ExpectedRepository) {
        $projectToml = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $Root 'project.toml')
        if ($projectToml -notmatch [regex]::Escape($ExpectedRepository)) {
            Add-Finding -Rule 'repository-name' -Path 'project.toml' -Line 1 `
                -Message "Expected repository name '$ExpectedRepository' is not recorded."
        }
        $oldSlugMatches = @(& git grep -nI 'tools-h000025-photoviewer' -- `
            ':!docs/publication-runbook.md' `
            ':!docs/public-repository-policy.md' 2>$null)
        if ($LASTEXITCODE -notin @(0, 1)) { throw 'git grep for the old repository slug failed.' }
        foreach ($match in $oldSlugMatches) {
            $parts = $match -split ':', 3
            Add-Finding -Rule 'old-repository-slug' -Path $parts[0] -Line ([int]$parts[1]) `
                -Message 'Old repository slug remains after rename.'
        }
    }

    $result = [pscustomobject]@{
        ok = $Findings.Count -eq 0
        mode = if ($FullTree) { 'full-tree' } else { 'public-surfaces' }
        requireLicense = [bool]$RequireLicense
        expectedRepository = $ExpectedRepository
        filesChecked = @($pathsToInspect).Count
        workflowFilesChecked = $workflowFiles.Count
        findings = @($Findings)
        warnings = @($Warnings)
    }
    $json = $result | ConvertTo-Json -Depth 8

    if ($OutputPath) {
        $resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
        $outputDirectory = Split-Path -Parent $resolvedOutput
        if ($outputDirectory) {
            New-Item -ItemType Directory -Force $outputDirectory | Out-Null
        }
        Set-Content -LiteralPath $resolvedOutput -Value $json -Encoding UTF8
    }
    Write-Output $json
    if (-not $result.ok) {
        throw 'Public surface verification failed. See the JSON findings above.'
    }
}
finally {
    Pop-Location
}
