$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$checker = Join-Path $PSScriptRoot 'check-wpf-launch-target.ps1'
$launcher = Join-Path $repoRoot 'start_wpf.bat'
$nativeLauncher = Join-Path $PSScriptRoot 'start-local-native.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('photoviewer-wpf-launcher-' + [Guid]::NewGuid().ToString('N'))
$projectRoot = Join-Path $fixtureRoot 'PhotoViewer.Wpf'
$project = Join-Path $projectRoot 'PhotoViewer.Wpf.csproj'
$source = Join-Path $projectRoot 'MainWindow.xaml'
$target = Join-Path $projectRoot 'bin\Release\net8.0-windows\PhotoViewer.Wpf.exe'
$provenance = $target + '.launch.json'

function Invoke-Check([switch]$Record) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $checker, '-ProjectPath', $project, '-TargetPath', $target, '-Json')
    if ($Record) { $arguments += '-Record' }
    & powershell.exe @arguments | Out-Null
    return $LASTEXITCODE
}

function Invoke-CheckPayload {
    $output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $checker -ProjectPath $project -TargetPath $target -Json
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Payload = ($output | Out-String | ConvertFrom-Json)
    }
}

try {
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Set-Content -LiteralPath $project -Value '<Project Sdk="Microsoft.NET.Sdk" />' -Encoding utf8
    Set-Content -LiteralPath $source -Value '<Window />' -Encoding utf8

    $missingExit = Invoke-Check
    if ($missingExit -ne 10) {
        throw "Missing target returned $missingExit instead of 10."
    }

    Set-Content -LiteralPath $target -Value 'fixture-v1' -Encoding ascii
    (Get-Item -LiteralPath $project).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(-3)
    (Get-Item -LiteralPath $source).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(-2)
    (Get-Item -LiteralPath $target).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(5)
    $unverifiedExit = Invoke-Check
    if ($unverifiedExit -ne 10) {
        throw "A newer but unproven target returned $unverifiedExit instead of 10."
    }

    $recordExit = Invoke-Check -Record
    if ($recordExit -ne 0 -or -not (Test-Path -LiteralPath $provenance)) {
        throw "Recording provenance failed with $recordExit."
    }
    $currentExit = Invoke-Check
    if ($currentExit -ne 0) {
        throw "A recorded current target returned $currentExit instead of 0."
    }

    $generatedSource = Join-Path $projectRoot 'obj\Release\Generated.cs'
    New-Item -ItemType Directory -Path (Split-Path -Parent $generatedSource) -Force | Out-Null
    Set-Content -LiteralPath $generatedSource -Value '// generated output must not define source freshness' -Encoding utf8
    if ((Invoke-Check) -ne 0) {
        throw 'Generated bin/obj output incorrectly invalidated source provenance.'
    }
    $transientWpfProject = Join-Path $projectRoot 'PhotoViewer.Wpf_fixture_wpftmp.csproj'
    Set-Content -LiteralPath $transientWpfProject -Value '<Project Sdk="Microsoft.NET.Sdk" />' -Encoding utf8
    if ((Invoke-Check) -ne 0) {
        throw 'Transient root-level *_wpftmp.csproj output incorrectly invalidated source provenance.'
    }

    Set-Content -LiteralPath $source -Value '<Window Title="changed" />' -Encoding utf8
    (Get-Item -LiteralPath $target).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(10)
    $sourceChanged = Invoke-CheckPayload
    if ($sourceChanged.ExitCode -ne 10 -or $sourceChanged.Payload.reason -ne 'source-content-mismatch') {
        throw "Source content drift was not rejected: exit=$($sourceChanged.ExitCode), reason=$($sourceChanged.Payload.reason)."
    }
    if ((Invoke-Check -Record) -ne 0) { throw 'Could not refresh fixture provenance after source drift.' }

    $stamp = Get-Content -LiteralPath $provenance -Raw -Encoding UTF8 | ConvertFrom-Json
    $stamp.sourceRevision = '0000000000000000000000000000000000000000'
    $stamp | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $provenance -Encoding utf8
    $revisionChanged = Invoke-CheckPayload
    if ($revisionChanged.ExitCode -ne 10 -or $revisionChanged.Payload.reason -ne 'source-revision-mismatch') {
        throw "Wrong-revision provenance was not rejected: exit=$($revisionChanged.ExitCode), reason=$($revisionChanged.Payload.reason)."
    }
    if ((Invoke-Check -Record) -ne 0) { throw 'Could not refresh fixture provenance after revision drift.' }

    $stamp = Get-Content -LiteralPath $provenance -Raw -Encoding UTF8 | ConvertFrom-Json
    $stamp.repoRoot = Join-Path $fixtureRoot 'wrong-worktree'
    $stamp | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $provenance -Encoding utf8
    $rootChanged = Invoke-CheckPayload
    if ($rootChanged.ExitCode -ne 10 -or $rootChanged.Payload.reason -ne 'repo-root-mismatch') {
        throw "Wrong-worktree provenance was not rejected: exit=$($rootChanged.ExitCode), reason=$($rootChanged.Payload.reason)."
    }
    if ((Invoke-Check -Record) -ne 0) { throw 'Could not refresh fixture provenance after root drift.' }

    Add-Content -LiteralPath $target -Value 'tampered' -Encoding ascii
    $targetChanged = Invoke-CheckPayload
    if ($targetChanged.ExitCode -ne 10 -or $targetChanged.Payload.reason -ne 'target-hash-mismatch') {
        throw "Target tampering was not rejected: exit=$($targetChanged.ExitCode), reason=$($targetChanged.Payload.reason)."
    }

    Set-Content -LiteralPath $provenance -Value '{broken' -Encoding ascii
    $invalidStamp = Invoke-CheckPayload
    if ($invalidStamp.ExitCode -ne 10 -or $invalidStamp.Payload.reason -ne 'provenance-invalid') {
        throw "Malformed provenance was not rejected: exit=$($invalidStamp.ExitCode), reason=$($invalidStamp.Payload.reason)."
    }

    $launcherText = Get-Content -LiteralPath $launcher -Raw
    if ($launcherText -notmatch 'cd /d "%~dp0"' -or $launcherText -notmatch '"%TARGET%" %\*') {
        throw 'start_wpf.bat does not pin the repo working directory and direct Release executable.'
    }
    if ($launcherText -notmatch 'check-wpf-launch-target\.ps1' -or $launcherText -notmatch 'CHECK_CODE') {
        throw 'start_wpf.bat does not enforce the provenance check.'
    }
    if ($launcherText -notmatch 'dotnet build' -or $launcherText -notmatch 'if errorlevel 1 exit /b %ERRORLEVEL%' -or $launcherText -notmatch '-Record') {
        throw 'start_wpf.bat does not fail closed before recording successful build provenance.'
    }
    if ($launcherText -match 'if not "%ERRORLEVEL%"' -or $launcherText -match '(?i)taskkill|stop-process') {
        throw 'start_wpf.bat has unsafe exit-code capture or process cleanup.'
    }
    $exitSectionMatch = [regex]::Match(
        $launcherText,
        '(?ms)^:exit_with_code\r?\n(?<body>.*?)(?=^:build_target\r?$)')
    if (-not $exitSectionMatch.Success) {
        throw 'start_wpf.bat does not expose a bounded exit-code section.'
    }
    $exitSection = $exitSectionMatch.Groups['body'].Value
    if ($exitSection -notmatch '(?m)^if "%EXIT_CODE%"=="0" exit /b 0\r?$' -or
        $exitSection -notmatch '(?m)^pause\r?$' -or
        $exitSection -notmatch '(?m)^exit /b %EXIT_CODE%\r?$') {
        throw 'start_wpf.bat does not exit immediately on success while preserving the visible nonzero failure path.'
    }
    $successExitOffset = $exitSection.IndexOf('if "%EXIT_CODE%"=="0" exit /b 0', [StringComparison]::Ordinal)
    $pauseOffset = $exitSection.IndexOf('pause', [StringComparison]::OrdinalIgnoreCase)
    if ($successExitOffset -lt 0 -or $pauseOffset -lt 0 -or $successExitOffset -gt $pauseOffset) {
        throw 'start_wpf.bat can pause before returning a successful application exit.'
    }
    $pauseLines = [regex]::Matches($launcherText, '(?m)^\s*pause\r?$')
    if ($pauseLines.Count -ne 2 -or
        $launcherText -notmatch '(?ms)^if not exist "%PROJECT%" \(\r?\n.*?^\s*pause\r?\n\s*exit /b 1\r?\n\)') {
        throw 'start_wpf.bat must reserve pause only for a missing project or a nonzero application exit.'
    }
    if ($launcherText -match '(?i)node(?:\.exe)?|localhost|127\.0\.0\.1') {
        throw 'start_wpf.bat unexpectedly depends on the Browser/Node runtime.'
    }

    $nativeLauncherText = Get-Content -LiteralPath $nativeLauncher -Raw
    if ($nativeLauncherText -notmatch 'PhotoViewer\.Native\.csproj' -or $nativeLauncherText -match 'PhotoViewer\.Wpf|start_wpf|(?i)node(?:\.exe)?|localhost|127\.0\.0\.1') {
        throw 'start-local-native.ps1 no longer remains isolated to the WinForms/.NET launcher.'
    }

    [pscustomobject]@{
        ok = $true
        missingExit = $missingExit
        unprovenNewerTargetExit = $unverifiedExit
        currentExit = $currentExit
        sourceContentMismatchRejected = $true
        sourceRevisionMismatchRejected = $true
        worktreeRootMismatchRejected = $true
        targetHashMismatchRejected = $true
        invalidProvenanceRejected = $true
        generatedOutputIgnored = $true
        transientWpfProjectIgnored = $true
        buildFailureFailsClosed = $true
        successfulExitDoesNotPause = $true
        nonzeroExitRemainsVisible = $true
        duplicateProcessesNotKilled = $true
        exactWorkingDirectory = $true
        nativeLauncherIsolated = $true
        nodeOrLocalhostDependency = $false
        tempOnly = $true
    } | ConvertTo-Json -Depth 3
}
finally {
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
}
