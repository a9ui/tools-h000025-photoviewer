param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj"
$exe = Join-Path $repoRoot "local-native\PhotoViewer.Wpf\bin\$Configuration\net8.0-windows\PhotoViewer.Wpf.exe"
$fixture = Join-Path $repoRoot "local-native\ui-mockup"
$root = Join-Path $env:TEMP ("photoviewer-wpf-automation-isolation-" + [guid]::NewGuid().ToString("N"))

dotnet build $project -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Path $root -Force | Out-Null
$sentinels = [ordered]@{
    PHOTOVIEWER_WPF_STATE_PATH = Join-Path $root "sentinel-state.json"
    PHOTOVIEWER_WPF_FAVORITES_PATH = Join-Path $root "sentinel-favorites.json"
    PHOTOVIEWER_WPF_SEEN_PATH = Join-Path $root "sentinel-seen.json"
    PHOTOVIEWER_WPF_RECENT_PATH = Join-Path $root "sentinel-recent.json"
    PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH = Join-Path $root "sentinel-jobs.json"
}
$contents = @{
    PHOTOVIEWER_WPF_STATE_PATH = '{"Version":1,"sentinel":"state"}'
    PHOTOVIEWER_WPF_FAVORITES_PATH = '{"C:\\sentinel.png":3}'
    PHOTOVIEWER_WPF_SEEN_PATH = '{"C:\\sentinel.png":true}'
    PHOTOVIEWER_WPF_RECENT_PATH = '{"version":1,"lastFolderSet":[],"recentFolderSets":[],"updatedAtUtc":"2026-01-01T00:00:00Z"}'
    PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH = '{"jobs":[]}'
}

foreach ($name in $sentinels.Keys) {
    Set-Content -LiteralPath $sentinels[$name] -Value $contents[$name] -NoNewline -Encoding utf8
}
$before = @{}
foreach ($name in $sentinels.Keys) {
    $before[$name] = (Get-FileHash -LiteralPath $sentinels[$name] -Algorithm SHA256).Hash
}

$previous = @{}
foreach ($name in $sentinels.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
    [Environment]::SetEnvironmentVariable($name, $sentinels[$name], "Process")
}

try {
    $classification = Join-Path $root "classification.json"
    $classificationProcess = Start-Process -FilePath $exe `
        -ArgumentList @('--automation-isolation-smoke', ('"{0}"' -f $classification)) `
        -WindowStyle Hidden -PassThru -Wait
    if ($classificationProcess.ExitCode -ne 0) { throw "--automation-isolation-smoke exited with $($classificationProcess.ExitCode)" }
    $classificationResult = Get-Content -Raw -LiteralPath $classification | ConvertFrom-Json
    if (-not $classificationResult.ok -or -not $classificationResult.positionalFolderRemainsInteractive) {
        throw "Automation classification did not preserve positional folder semantics."
    }

    $shot = Join-Path $root "viewer.png"
    $process = Start-Process -FilePath $exe `
        -ArgumentList @('--shot', ('"{0}"' -f $shot), '--screen', 'viewer', '--folder', ('"{0}"' -f $fixture)) `
        -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "--shot exited with $($process.ExitCode)" }
    if (-not (Test-Path -LiteralPath $shot)) { throw "--shot did not create $shot" }
}
finally {
    foreach ($name in $sentinels.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], "Process")
    }
}

$after = @{}
foreach ($name in $sentinels.Keys) {
    $after[$name] = (Get-FileHash -LiteralPath $sentinels[$name] -Algorithm SHA256).Hash
}
$unchanged = @($sentinels.Keys | Where-Object { $before[$_] -eq $after[$_] })
if ($unchanged.Count -ne $sentinels.Count) {
    $changed = @($sentinels.Keys | Where-Object { $before[$_] -ne $after[$_] })
    throw "Automation screenshot mutated caller storage sentinel(s): $($changed -join ', ')"
}

[pscustomobject]@{
    ok = $true
    message = "--shot isolated state, favorites, seen, recent, and enhancement-job stores before WPF initialization"
    positionalFolderRemainsInteractive = [bool]$classificationResult.positionalFolderRemainsInteractive
    sentinels = $sentinels
    before = $before
    after = $after
} | ConvertTo-Json -Depth 4
