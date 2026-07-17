param(
    [switch]$SkipStress,
    [switch]$IncludeReloadSoak
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$self = Split-Path -Leaf $PSCommandPath
$powershell = (Get-Command powershell.exe -ErrorAction Stop).Source

$orderedNames = @(
    'verify-ui-regression-guard.ps1',
    'verify-wpf-p0.ps1',
    'verify-wpf-p1a.ps1',
    'verify-wpf-p1b.ps1',
    'verify-wpf-formats.ps1',
    'verify-wpf-decode-bounds.ps1',
    'verify-wpf-right-panel.ps1',
    'verify-wpf-bulk-favorite.ps1',
    'verify-wpf-bulk-recycle.ps1',
    'verify-wpf-folder-buckets.ps1',
    'verify-wpf-search-stall.ps1',
    'verify-wpf-preview-tabs.ps1',
    'verify-wpf-preview-tab-hover.ps1',
    'verify-wpf-preview-tab-reorder.ps1',
    'verify-wpf-modal-wrap.ps1',
    'verify-wpf-modal-interaction.ps1',
    'verify-wpf-catalog-stress.ps1'
)

$known = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($name in $orderedNames) { [void]$known.Add($name) }
[void]$known.Add('verify-wpf-reload-soak.ps1')

$discovered = Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'verify-wpf-*.ps1' -File |
    Where-Object { $_.Name -ne $self -and -not $known.Contains($_.Name) } |
    Sort-Object Name |
    Select-Object -ExpandProperty Name
$checks = @($orderedNames + $discovered)
if ($SkipStress) {
    $checks = @($checks | Where-Object { $_ -ne 'verify-wpf-catalog-stress.ps1' })
}
if ($IncludeReloadSoak) {
    $checks += 'verify-wpf-reload-soak.ps1'
}

$results = @()
$suiteWatch = [Diagnostics.Stopwatch]::StartNew()
Push-Location $repoRoot
try {
    foreach ($name in $checks) {
        $path = Join-Path $PSScriptRoot $name
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Required WPF verifier is missing: $name"
        }

        Write-Host "`n=== $name ===" -ForegroundColor Cyan
        $watch = [Diagnostics.Stopwatch]::StartNew()
        & $powershell -NoProfile -ExecutionPolicy Bypass -File $path
        $exitCode = $LASTEXITCODE
        $watch.Stop()
        $results += [pscustomobject]@{
            name = $name
            ok = $exitCode -eq 0
            exitCode = $exitCode
            elapsedMs = $watch.ElapsedMilliseconds
        }
        if ($exitCode -ne 0) {
            throw "$name failed with exit code $exitCode"
        }
    }
}
finally {
    Pop-Location
    $suiteWatch.Stop()
}

[pscustomobject]@{
    ok = $true
    checks = $results.Count
    skipStress = [bool]$SkipStress
    includeReloadSoak = [bool]$IncludeReloadSoak
    elapsedMs = $suiteWatch.ElapsedMilliseconds
    results = $results
} | ConvertTo-Json -Depth 5
