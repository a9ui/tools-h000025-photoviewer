param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$browserFiles = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\components') -Filter '*.tsx' -File |
    Where-Object { $_.Name -notlike '*.test.tsx' }
$targets = @($browserFiles.FullName) + @(
    (Join-Path $repoRoot 'src\app\page.tsx'),
    (Join-Path $repoRoot 'local-native\PhotoViewer.Wpf\MainWindow.xaml')
)
$runtimeTargets = @(
    (Join-Path $repoRoot 'local-native\PhotoViewer.Wpf\MainWindow.xaml.cs')
)

$forbidden = [ordered]@{
    'retired Quick Search control' = '(?i)quick\s*search'
    'retired relative-date preset control' = '(?i)(Content\s*=\s*"\s*(Today|7d|30d|This year)\s*"|>\s*(Today|7d|30d|This year)\s*<)'
    'retired Favorite threshold label' = '(?i)(Lv|Favorite|★|\*)\s*[1-5]\s*\+'
}

$findings = @()
foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target)) {
        throw "Required UI source is missing: $target"
    }
    foreach ($rule in $forbidden.GetEnumerator()) {
        $matches = Select-String -LiteralPath $target -Pattern $rule.Value -AllMatches
        foreach ($match in $matches) {
            $findings += [pscustomobject]@{
                Rule = $rule.Key
                File = [IO.Path]::GetRelativePath($repoRoot, $target)
                Line = $match.LineNumber
                Text = $match.Line.Trim()
            }
        }
    }
}

$runtimeForbidden = [ordered]@{
    'retired relative-date runtime preset API' = '(?i)(DatePresetTodayValue|DatePreset7DaysValue|DatePreset30DaysValue|DatePresetThisYearValue|DateRangeForPreset|SetDatePresetForSmoke)'
    'runtime state must not write an unchecked preset token' = 'DatePreset\s*=\s*_datePreset'
}
foreach ($target in $runtimeTargets) {
    if (-not (Test-Path -LiteralPath $target)) {
        throw "Required runtime source is missing: $target"
    }
    foreach ($rule in $runtimeForbidden.GetEnumerator()) {
        $matches = Select-String -LiteralPath $target -Pattern $rule.Value -AllMatches
        foreach ($match in $matches) {
            $findings += [pscustomobject]@{
                Rule = $rule.Key
                File = [IO.Path]::GetRelativePath($repoRoot, $target)
                Line = $match.LineNumber
                Text = $match.Line.Trim()
            }
        }
    }
}

if ($findings.Count -gt 0) {
    $findings | Format-Table -AutoSize | Out-String | Write-Host
    throw 'Retired PhotoViewer UI semantics were reintroduced.'
}

[pscustomobject]@{
    ok = $true
    filesChecked = $targets.Count + $runtimeTargets.Count
    rules = $forbidden.Keys
    message = 'Quick Search, relative-date controls, and Favorite threshold labels are absent from live Browser/WPF UI; runtime has no active relative-date preset API or unchecked preset writer.'
} | ConvertTo-Json -Depth 4
