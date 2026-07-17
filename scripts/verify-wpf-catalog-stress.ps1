param(
    [string]$Configuration = "Release",
    [int]$Count = 20000,
    [string]$OutputPath = (Join-Path $env:TEMP ("photoviewer-wpf-catalog-stress-" + [guid]::NewGuid().ToString('N') + ".json"))
)

$ErrorActionPreference = 'Stop'
if ($Count -lt 2) { throw 'Count must be at least 2.' }
if ($OutputPath.Contains('"')) { throw 'OutputPath cannot contain a double quote.' }

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj'
$exe = Join-Path $repoRoot "local-native\PhotoViewer.Wpf\bin\$Configuration\net8.0-windows\PhotoViewer.Wpf.exe"

dotnet build $project -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Remove-Item -LiteralPath $OutputPath -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $exe `
    -ArgumentList @('--catalog-stress-smoke', ('"{0}"' -f $OutputPath), '--count', $Count.ToString()) `
    -WindowStyle Hidden -Wait -PassThru

if (-not (Test-Path -LiteralPath $OutputPath)) {
    throw "WPF catalog stress process exited without producing $OutputPath"
}

$result = Get-Content -Raw -LiteralPath $OutputPath | ConvertFrom-Json
$structuralFailures = @()
if ($process.ExitCode -ne 0) { $structuralFailures += "process exit code $($process.ExitCode)" }
if ($result.ok -ne $true) { $structuralFailures += 'result.ok was false' }
if ($result.requestedCount -ne $Count) { $structuralFailures += "requested count was $($result.requestedCount)" }
if ($result.fixtureCount -ne $Count) { $structuralFailures += "fixture count was $($result.fixtureCount)" }
if ($result.catalogCount -ne $Count -or $result.filteredCount -ne $Count) { $structuralFailures += "catalog/filtered counts were $($result.catalogCount)/$($result.filteredCount)" }
if ($result.silentTruncateCount -ne 0) { $structuralFailures += "silent truncate count was $($result.silentTruncateCount)" }
if ($result.gridRealized -gt $result.gridMaximum) { $structuralFailures += "grid realization exceeded bound ($($result.gridRealized) > $($result.gridMaximum))" }
if ($result.gridDeferred -ne ($Count - $result.gridRealized)) { $structuralFailures += "grid deferred count was $($result.gridDeferred)" }
if ($result.listBounded -ne $true) { $structuralFailures += 'list realization was not recycling/bounded' }
if ($result.selectedTail -ne $true -or $result.modalTail -ne $true -or $result.finalSearchExact -ne $true) { $structuralFailures += 'tail search, selection, or modal reachability failed' }
if ($result.staleCancelled -ne $true -or $result.heartbeatCount -lt 4) { $structuralFailures += 'rapid-query cancellation or dispatcher heartbeat failed' }
if ($result.sourceCountAfter -ne $Count) { $structuralFailures += "source count changed to $($result.sourceCountAfter)" }
if ($result.enhancementJobsRead -ne 0 -or $result.enhancementCandidates -ne 0) { $structuralFailures += 'enhancement state was touched' }

$result | ConvertTo-Json -Depth 8
if ($structuralFailures.Count -gt 0) {
    throw ("WPF catalog stress structural gate failed: " + ($structuralFailures -join '; '))
}
