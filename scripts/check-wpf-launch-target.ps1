[CmdletBinding()]
param(
    [string]$ProjectPath = 'local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj',
    [string]$TargetPath = 'local-native\PhotoViewer.Wpf\bin\Release\net8.0-windows\PhotoViewer.Wpf.exe',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-RepoPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

try {
    $project = Resolve-RepoPath $ProjectPath
    $target = Resolve-RepoPath $TargetPath

    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw "WPF project was not found: $project"
    }

    $projectRoot = Split-Path -Parent $project
    $targetItem = Get-Item -LiteralPath $target -ErrorAction SilentlyContinue
    $buildExtensions = @('.cs', '.xaml', '.csproj', '.manifest', '.resx', '.props', '.targets')
    $latestInput = Get-ChildItem -LiteralPath $projectRoot -Recurse -File |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
            $buildExtensions -contains $_.Extension.ToLowerInvariant()
        } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    $status = if ($null -eq $targetItem) {
        'missing'
    }
    elseif ($null -ne $latestInput -and $latestInput.LastWriteTimeUtc -gt $targetItem.LastWriteTimeUtc) {
        'stale'
    }
    else {
        'current'
    }

    $result = [pscustomobject]@{
        status = $status
        target = $target
        targetLastWriteUtc = if ($null -ne $targetItem) { $targetItem.LastWriteTimeUtc.ToString('o') } else { $null }
        newestInput = if ($null -ne $latestInput) { $latestInput.FullName } else { $null }
        newestInputLastWriteUtc = if ($null -ne $latestInput) { $latestInput.LastWriteTimeUtc.ToString('o') } else { $null }
    }

    if ($Json) {
        $result | ConvertTo-Json -Depth 3
    }
    elseif ($status -eq 'current') {
        Write-Host '[PhotoViewer WPF] Release executable is current.'
    }
    else {
        Write-Host "[PhotoViewer WPF] Release executable is $status; rebuilding before launch."
    }

    if ($status -eq 'current') {
        exit 0
    }

    exit 10
}
catch {
    [Console]::Error.WriteLine("[PhotoViewer WPF] Freshness check failed: $($_.Exception.Message)")
    exit 2
}
