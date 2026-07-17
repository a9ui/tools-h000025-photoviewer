param(
    [string]$Configuration = 'Release',
    [string]$OutputPath = (Join-Path $env:TEMP ('photoviewer-wpf-shared-state-latency-' + [guid]::NewGuid().ToString('N') + '.json')),
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj'
$exe = Join-Path $repoRoot "local-native\PhotoViewer.Wpf\bin\$Configuration\net8.0-windows\PhotoViewer.Wpf.exe"
$tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$outputFullPath = [IO.Path]::GetFullPath($OutputPath)
if (-not $outputFullPath.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must stay under TEMP: $outputFullPath"
}
if ($outputFullPath.Contains('"')) { throw 'OutputPath cannot contain a double quote.' }

if (-not $NoBuild) {
    dotnet build $project -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if (-not (Test-Path -LiteralPath $exe)) { throw "WPF executable is missing: $exe" }

Remove-Item -LiteralPath $outputFullPath -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $exe `
    -ArgumentList @('--shared-state-latency-smoke', ('"{0}"' -f $outputFullPath)) `
    -WindowStyle Hidden -Wait -PassThru

if (-not (Test-Path -LiteralPath $outputFullPath)) {
    throw "WPF shared-state latency process exited without producing $outputFullPath"
}

$result = Get-Content -Raw -LiteralPath $outputFullPath | ConvertFrom-Json
$failures = @()
if ($process.ExitCode -ne 0) { $failures += "process exit code $($process.ExitCode)" }
if ($result.ok -ne $true) { $failures += 'result.ok was false' }
if ($result.profiles.Count -ne 2) { $failures += 'small/large profile pair missing' }
$small = @($result.profiles | Where-Object name -eq 'small')[0]
$large = @($result.profiles | Where-Object name -eq 'large-100000')[0]
if ($null -eq $small -or $null -eq $large) { $failures += 'named profiles missing' }

foreach ($profile in @($small, $large)) {
    if ($null -eq $profile) { continue }
    if ($profile.ok -ne $true -or $profile.actionsAccepted -ne $true) { $failures += "$($profile.name): normal action path failed" }
    if ($profile.favoriteExact -ne $true -or $profile.seenAdditive -ne $true -or $profile.repeatedSeenNoWrite -ne $true) {
        $failures += "$($profile.name): Favorite/Seen exactness failed"
    }
    if ($profile.sourceUntouched -ne $true -or $profile.jobsUntouched -ne $true -or $profile.enhancementPassive -ne $true) {
        $failures += "$($profile.name): source/Enhancement isolation failed"
    }
    if ($profile.isolated -ne $true -or $profile.residueFree -ne $true -or $profile.pendingAtClose -ne $false) {
        $failures += "$($profile.name): temp/residue/lifecycle boundary failed"
    }
    if ($profile.modalNext.rawMs.Count -ne 20 -or $profile.favoriteAction.rawMs.Count -ne 20) {
        $failures += "$($profile.name): expected 20 raw modal/Favorite samples"
    }
    if ($profile.idleHeartbeat.count -lt 3 -or $profile.workloadHeartbeat.count -lt 2) {
        $failures += "$($profile.name): dispatcher heartbeat evidence missing"
    }
}

if ($null -ne $large) {
    if ($large.favoriteSeedEntries -ne 100000 -or $large.seenSeedEntries -ne 100000) {
        $failures += 'large profile did not seed exactly 100000 Favorite and Seen entries'
    }
    $twoMiB = 2MB
    $fiveMiB = 5MB
    if ($large.favoriteSeedBytes -lt $twoMiB -or $large.favoriteSeedBytes -gt $fiveMiB `
        -or $large.seenSeedBytes -lt $twoMiB -or $large.seenSeedBytes -gt $fiveMiB) {
        $failures += 'large profile bytes were not in the expected approximately-2.8MiB envelope'
    }
}

if ([string]::IsNullOrWhiteSpace($result.smokeRoot) -or (Test-Path -LiteralPath $result.smokeRoot)) {
    $failures += 'temp smoke root was not cleaned after the isolated process exited'
}

$result | ConvertTo-Json -Depth 12
if ($failures.Count -gt 0) {
    throw ('WPF shared-state latency gate failed: ' + ($failures -join '; '))
}
