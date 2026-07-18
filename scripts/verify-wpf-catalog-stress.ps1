param(
    [string]$Configuration = "Release",
    [int]$Count = 20000,
    [string]$OutputPath = (Join-Path $env:TEMP ("photoviewer-wpf-catalog-stress-" + [guid]::NewGuid().ToString('N') + ".json")),
    [int]$UnresponsiveStreakLimitMs = 750,
    [int]$OverallTimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
if ($Count -lt 2) { throw 'Count must be at least 2.' }
if ($OutputPath.Contains('"')) { throw 'OutputPath cannot contain a double quote.' }
if ($UnresponsiveStreakLimitMs -lt 1) { throw 'UnresponsiveStreakLimitMs must be positive.' }
if ($OverallTimeoutSeconds -lt 1) { throw 'OverallTimeoutSeconds must be positive.' }

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj'
$exe = Join-Path $repoRoot "local-native\PhotoViewer.Wpf\bin\$Configuration\net8.0-windows\PhotoViewer.Wpf.exe"
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
$tempRootPrefix = $tempRoot + [IO.Path]::DirectorySeparatorChar
$outputFullPath = [IO.Path]::GetFullPath($OutputPath)
if (-not $outputFullPath.StartsWith($tempRootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must stay under TEMP: $outputFullPath"
}

dotnet build $project -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Remove-Item -LiteralPath $outputFullPath -Force -ErrorAction SilentlyContinue
if (-not ('PhotoViewerWmNullProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class PhotoViewerWmNullProbe
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);
}
'@
}

$process = $null
$spawnedProcessId = $null
$processExitCode = $null
$processTerminatedByVerifier = $false
$monitorError = $null
$timedOut = $false
$resultObservedBeforeExit = $false
$overallWatch = [Diagnostics.Stopwatch]::new()
$probeWatch = [Diagnostics.Stopwatch]::new()
$windowHandleSeenMs = $null
$probeCount = 0
$probeTimeoutCount = 0
$currentUnresponsiveStartMs = $null
$longestUnresponsiveStreakMs = 0L
$unresponsiveSegments = [Collections.Generic.List[object]]::new()
$overallTimeoutMs = [int64]$OverallTimeoutSeconds * 1000
try {
    $process = Start-Process -FilePath $exe `
        -ArgumentList @('--catalog-stress-smoke', ('"{0}"' -f $outputFullPath), '--count', $Count.ToString()) `
        -WindowStyle Hidden -PassThru
    $spawnedProcessId = $process.Id
    $overallWatch.Start()
    $probeWatch.Start()

    while ($true) {
        $process.Refresh()
        $hasExited = $process.HasExited
        if (Test-Path -LiteralPath $outputFullPath) {
            $resultObservedBeforeExit = -not $hasExited
            break
        }
        if ($hasExited) {
            break
        }
        if ($overallWatch.ElapsedMilliseconds -ge $overallTimeoutMs) {
            $timedOut = $true
            break
        }

        $windowHandle = $process.MainWindowHandle
        if ($windowHandle -ne [IntPtr]::Zero) {
            if ($null -eq $windowHandleSeenMs) {
                $windowHandleSeenMs = $probeWatch.ElapsedMilliseconds
            }

            $probeStartMs = $probeWatch.ElapsedMilliseconds
            $messageResult = [IntPtr]::Zero
            # WM_NULL + SMTO_BLOCK | SMTO_ABORTIFHUNG | SMTO_ERRORONEXIT.
            $responsive = [PhotoViewerWmNullProbe]::SendMessageTimeout(
                $windowHandle,
                0,
                [IntPtr]::Zero,
                [IntPtr]::Zero,
                0x23,
                100,
                [ref]$messageResult)
            $probeEndMs = $probeWatch.ElapsedMilliseconds
            $probeCount++
            if ($responsive -eq [IntPtr]::Zero) {
                $probeTimeoutCount++
                if ($null -eq $currentUnresponsiveStartMs) {
                    $currentUnresponsiveStartMs = $probeStartMs
                }
                $longestUnresponsiveStreakMs = [Math]::Max($longestUnresponsiveStreakMs, $probeEndMs - $currentUnresponsiveStartMs)
            }
            else {
                if ($null -ne $currentUnresponsiveStartMs) {
                    $segmentDurationMs = $probeStartMs - $currentUnresponsiveStartMs
                    $unresponsiveSegments.Add([pscustomobject]@{
                        startMs = $currentUnresponsiveStartMs
                        endMs = $probeStartMs
                        durationMs = $segmentDurationMs
                    })
                    $longestUnresponsiveStreakMs = [Math]::Max($longestUnresponsiveStreakMs, $segmentDurationMs)
                }
                $currentUnresponsiveStartMs = $null
            }
        }

        Start-Sleep -Milliseconds 15
    }

    # The app writes its result before deleting the isolated 20k fixture. Stop
    # measuring UI liveness at that contract boundary, then bound cleanup by the
    # same overall deadline.
    if ($probeWatch.IsRunning) {
        $probeWatch.Stop()
    }
    $process.Refresh()
    if (-not $process.HasExited -and -not $timedOut) {
        $remainingMs = [Math]::Max(0, $overallTimeoutMs - $overallWatch.ElapsedMilliseconds)
        if ($remainingMs -le 0 -or -not $process.WaitForExit([int][Math]::Min([int]::MaxValue, $remainingMs))) {
            $timedOut = $true
        }
    }
}
catch {
    $monitorError = $_.Exception.GetType().Name + ': ' + $_.Exception.Message
}
finally {
    if ($probeWatch.IsRunning) {
        $probeWatch.Stop()
    }
    if ($null -ne $currentUnresponsiveStartMs) {
        $segmentEndMs = $probeWatch.ElapsedMilliseconds
        $unresponsiveSegments.Add([pscustomobject]@{
            startMs = $currentUnresponsiveStartMs
            endMs = $segmentEndMs
            durationMs = $segmentEndMs - $currentUnresponsiveStartMs
        })
        $longestUnresponsiveStreakMs = [Math]::Max($longestUnresponsiveStreakMs, $segmentEndMs - $currentUnresponsiveStartMs)
    }

    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            $processTerminatedByVerifier = $true
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            [void]$process.WaitForExit(5000)
            $process.Refresh()
        }
        if ($process.HasExited) {
            $processExitCode = $process.ExitCode
        }
    }
    if ($overallWatch.IsRunning) {
        $overallWatch.Stop()
    }
}

$resultReadError = $null
if (Test-Path -LiteralPath $outputFullPath) {
    try {
        $result = Get-Content -Raw -LiteralPath $outputFullPath | ConvertFrom-Json
    }
    catch {
        $resultReadError = $_.Exception.GetType().Name + ': ' + $_.Exception.Message
        $result = [pscustomobject]@{ ok = $false }
    }
}
else {
    $result = [pscustomobject]@{ ok = $false }
}

$result | Add-Member -Force -NotePropertyName spawnedProcessId -NotePropertyValue $spawnedProcessId
$result | Add-Member -Force -NotePropertyName processExitCode -NotePropertyValue $processExitCode
$result | Add-Member -Force -NotePropertyName processTerminatedByVerifier -NotePropertyValue $processTerminatedByVerifier
$result | Add-Member -Force -NotePropertyName timedOut -NotePropertyValue $timedOut
$result | Add-Member -Force -NotePropertyName overallTimeoutSeconds -NotePropertyValue $OverallTimeoutSeconds
$result | Add-Member -Force -NotePropertyName overallElapsedMs -NotePropertyValue $overallWatch.ElapsedMilliseconds
$result | Add-Member -Force -NotePropertyName resultObservedBeforeExit -NotePropertyValue $resultObservedBeforeExit
$result | Add-Member -Force -NotePropertyName monitorError -NotePropertyValue $monitorError
$result | Add-Member -Force -NotePropertyName resultReadError -NotePropertyValue $resultReadError
$result | Add-Member -Force -NotePropertyName externalProbeElapsedMs -NotePropertyValue $probeWatch.ElapsedMilliseconds
$result | Add-Member -Force -NotePropertyName windowHandleSeenMs -NotePropertyValue $windowHandleSeenMs
$result | Add-Member -Force -NotePropertyName wmNullProbeCount -NotePropertyValue $probeCount
$result | Add-Member -Force -NotePropertyName wmNullTimeoutCount -NotePropertyValue $probeTimeoutCount
$result | Add-Member -Force -NotePropertyName maxUnresponsiveStreakMs -NotePropertyValue $longestUnresponsiveStreakMs
$result | Add-Member -Force -NotePropertyName maxAllowedUnresponsiveStreakMs -NotePropertyValue $UnresponsiveStreakLimitMs
$result | Add-Member -Force -NotePropertyName unresponsiveSegments -NotePropertyValue @($unresponsiveSegments)
$structuralFailures = @()
if ($timedOut) { $structuralFailures += "overall deadline of $OverallTimeoutSeconds seconds expired" }
if ($null -ne $monitorError) { $structuralFailures += "monitor failed: $monitorError" }
if ($null -ne $resultReadError) { $structuralFailures += "result JSON could not be read: $resultReadError" }
if (-not (Test-Path -LiteralPath $outputFullPath)) { $structuralFailures += "process did not produce $outputFullPath" }
if ($processTerminatedByVerifier -and -not $timedOut -and $null -eq $monitorError) { $structuralFailures += 'process required verifier cleanup before the deadline' }
if ($null -ne $processExitCode -and $processExitCode -ne 0 -and -not $processTerminatedByVerifier) { $structuralFailures += "process exit code $processExitCode" }
if ($result.ok -ne $true) { $structuralFailures += 'result.ok was false' }
if ($result.cleanupBoundaryPublished -ne $true) {
    $structuralFailures += 'pre-cleanup liveness boundary was not published'
}
if ($result.cleanupAttempted -ne $true -or $result.cleanupSucceeded -ne $true -or -not [string]::IsNullOrWhiteSpace($result.cleanupError)) {
    $structuralFailures += "temp fixture cleanup did not succeed: $($result.cleanupError)"
}
$cleanupRootFull = $null
try {
    if ([string]::IsNullOrWhiteSpace($result.smokeRoot)) {
        throw 'result smokeRoot was empty'
    }
    $cleanupRootFull = [IO.Path]::GetFullPath([string]$result.smokeRoot)
    $cleanupLeaf = [IO.Path]::GetFileName($cleanupRootFull.TrimEnd('\', '/'))
    $cleanupParent = [IO.Path]::GetDirectoryName($cleanupRootFull.TrimEnd('\', '/'))
    if (-not [string]::Equals($cleanupParent, $tempRoot, [StringComparison]::OrdinalIgnoreCase) `
        -or $cleanupLeaf -notmatch '^photoviewer-wpf-catalog-stress-[0-9a-f]{32}$') {
        throw "cleanup root was not the exact generated TEMP fixture root: $cleanupRootFull"
    }
    if (Test-Path -LiteralPath $cleanupRootFull) {
        throw "cleanup root still exists: $cleanupRootFull"
    }
}
catch {
    $structuralFailures += $_.Exception.Message
}
if ($result.requestedCount -ne $Count) { $structuralFailures += "requested count was $($result.requestedCount)" }
if ($result.fixtureCount -ne $Count) { $structuralFailures += "fixture count was $($result.fixtureCount)" }
if ($result.catalogCount -ne $Count -or $result.filteredCount -ne $Count) { $structuralFailures += "catalog/filtered counts were $($result.catalogCount)/$($result.filteredCount)" }
if ($result.silentTruncateCount -ne 0) { $structuralFailures += "silent truncate count was $($result.silentTruncateCount)" }
if ($result.gridRealized -gt $result.gridMaximum) { $structuralFailures += "grid realization exceeded bound ($($result.gridRealized) > $($result.gridMaximum))" }
if ($result.gridDeferred -ne ($Count - $result.gridRealized)) { $structuralFailures += "grid deferred count was $($result.gridDeferred)" }
if ($result.listBounded -ne $true) { $structuralFailures += 'list realization was not recycling/bounded' }
if ($result.selectedTail -ne $true -or $result.tailCanonicalSelected -ne $true) { $structuralFailures += 'tail canonical selection failed' }
if ($result.tailGridWindowContains -ne $true -or $result.tailCardsSelectedItemsContains -ne $true -or $result.tailGridContainerRealized -ne $true -or $result.tailGridContainerSelected -ne $true) { $structuralFailures += 'tail grid visual selection/container failed' }
if ($result.tailListModeRoundTrip -ne $true -or $result.tailListCanonicalSelected -ne $true -or $result.tailRowsSelectedItemsContains -ne $true -or $result.tailListContainerRealized -ne $true -or $result.tailListContainerSelected -ne $true) { $structuralFailures += 'tail List-mode canonical/visual round trip failed' }
if ($result.tailGridModeRoundTrip -ne $true -or $result.tailGridRoundTripCanonicalSelected -ne $true -or $result.tailGridRoundTripWindowContains -ne $true -or $result.tailCardsRoundTripSelectedItemsContains -ne $true -or $result.tailGridRoundTripContainerRealized -ne $true -or $result.tailGridRoundTripContainerSelected -ne $true) { $structuralFailures += 'tail Grid-mode canonical/visual round trip failed' }
if ($result.modalTail -ne $true -or $result.finalSearchExact -ne $true) { $structuralFailures += 'tail search or modal reachability failed' }
if ($result.staleCancelled -ne $true -or $result.heartbeatCount -lt 4) { $structuralFailures += 'rapid-query cancellation or dispatcher heartbeat failed' }
if ($result.sourceCountAfter -ne $Count) { $structuralFailures += "source count changed to $($result.sourceCountAfter)" }
if ($result.enhancementJobsRead -ne 0 -or $result.enhancementCandidates -ne 0) { $structuralFailures += 'enhancement state was touched' }
if ($null -eq $windowHandleSeenMs -or $probeCount -lt 1) { $structuralFailures += 'external WM_NULL probe never observed the WPF window' }
if ($longestUnresponsiveStreakMs -gt $UnresponsiveStreakLimitMs) { $structuralFailures += "external WM_NULL unresponsive streak was $longestUnresponsiveStreakMs ms (limit $UnresponsiveStreakLimitMs ms)" }
if ($null -eq $result.dispatcherHeartbeatMaxGapMs -or $result.dispatcherHeartbeatMaxGapMs -gt $UnresponsiveStreakLimitMs) { $structuralFailures += "dispatcher heartbeat gap was $($result.dispatcherHeartbeatMaxGapMs) ms (limit $UnresponsiveStreakLimitMs ms)" }

$resultJson = $result | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText($outputFullPath, $resultJson, [Text.UTF8Encoding]::new($false))
$resultJson
if ($structuralFailures.Count -gt 0) {
    throw ("WPF catalog stress structural gate failed: " + ($structuralFailures -join '; '))
}
