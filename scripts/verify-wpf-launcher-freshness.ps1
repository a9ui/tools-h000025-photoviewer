$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$checker = Join-Path $PSScriptRoot 'check-wpf-launch-target.ps1'
$launcher = Join-Path $repoRoot 'start_wpf.bat'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('photoviewer-wpf-launcher-' + [Guid]::NewGuid().ToString('N'))
$projectRoot = Join-Path $fixtureRoot 'PhotoViewer.Wpf'
$project = Join-Path $projectRoot 'PhotoViewer.Wpf.csproj'
$source = Join-Path $projectRoot 'MainWindow.xaml'
$target = Join-Path $projectRoot 'bin\Release\net8.0-windows\PhotoViewer.Wpf.exe'

function Invoke-Check {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $checker -ProjectPath $project -TargetPath $target -Json | Out-Null
    return $LASTEXITCODE
}

try {
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Set-Content -LiteralPath $project -Value '<Project Sdk="Microsoft.NET.Sdk" />' -Encoding utf8
    Set-Content -LiteralPath $source -Value '<Window />' -Encoding utf8

    $missingExit = Invoke-Check
    if ($missingExit -ne 10) {
        throw "Missing target returned $missingExit instead of 10."
    }

    Set-Content -LiteralPath $target -Value 'fixture' -Encoding ascii
    (Get-Item -LiteralPath $project).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(-3)
    (Get-Item -LiteralPath $source).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(-2)
    (Get-Item -LiteralPath $target).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(-1)
    $currentExit = Invoke-Check
    if ($currentExit -ne 0) {
        throw "Current target returned $currentExit instead of 0."
    }

    (Get-Item -LiteralPath $source).LastWriteTimeUtc = [DateTime]::UtcNow
    $staleExit = Invoke-Check
    if ($staleExit -ne 10) {
        throw "Stale target returned $staleExit instead of 10."
    }

    $launcherText = Get-Content -LiteralPath $launcher -Raw
    if ($launcherText -notmatch 'check-wpf-launch-target\.ps1' -or $launcherText -notmatch 'CHECK_CODE') {
        throw 'start_wpf.bat does not enforce the freshness check.'
    }
    if ($launcherText -match 'if not "%ERRORLEVEL%"') {
        throw 'start_wpf.bat captures ERRORLEVEL before the build command runs.'
    }

    [pscustomobject]@{
        ok = $true
        missingExit = $missingExit
        currentExit = $currentExit
        staleExit = $staleExit
        tempOnly = $true
    } | ConvertTo-Json -Depth 3
}
finally {
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
}
