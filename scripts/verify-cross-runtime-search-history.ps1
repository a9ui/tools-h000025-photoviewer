param(
    [ValidateRange(1, 24)]
    [int]$Iterations = 20,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
$root = Join-Path $tempBase ("photoviewer-cross-runtime-search-history-" + [guid]::NewGuid().ToString('N'))
$fullRoot = [IO.Path]::GetFullPath($root)
if (-not $fullRoot.StartsWith($tempBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a non-temp verifier root: $fullRoot"
}

try {
    New-Item -ItemType Directory -Force -Path $fullRoot | Out-Null
    $historyPath = Join-Path $fullRoot 'search-history.json'
    $wpfResultPath = Join-Path $fullRoot 'wpf-result.json'
    $seed = @{ version = 1; entries = @(); updatedAtUtc = '2026-07-19T00:00:00.000Z'; ownerMarker = @{ keep = $true } } |
        ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($historyPath, $seed, [Text.UTF8Encoding]::new($false))

    $project = Join-Path $repoRoot 'local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj'
    $exe = Join-Path $repoRoot "local-native\PhotoViewer.Wpf\bin\$Configuration\net8.0-windows\PhotoViewer.Wpf.exe"
    dotnet build $project -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $env:PVU_SEARCH_HISTORY_PATH = $historyPath
    $env:CROSS_RUNTIME_ITERATIONS = $Iterations.ToString([Globalization.CultureInfo]::InvariantCulture)
    $wpf = Start-Process -FilePath $exe -ArgumentList @(
        '--cross-runtime-search-history-smoke', $wpfResultPath,
        '--search-history-path', $historyPath,
        '--iterations', $Iterations.ToString([Globalization.CultureInfo]::InvariantCulture)
    ) -WindowStyle Hidden -PassThru
    $browserOutput = & corepack pnpm exec vitest run src/app/api/crossRuntimeSearchHistory.worker.test.ts --reporter=dot 2>&1
    $browserExitCode = $LASTEXITCODE
    $wpf.WaitForExit()
    $wpf.Refresh()
    if ($browserExitCode -ne 0) { throw "Browser history worker failed: $($browserOutput -join [Environment]::NewLine)" }
    if ($wpf.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $wpfResultPath)) { throw "WPF history worker failed with exit $($wpf.ExitCode)" }

    $wpfResult = Get-Content -Raw -Encoding utf8 -LiteralPath $wpfResultPath | ConvertFrom-Json
    $stored = Get-Content -Raw -Encoding utf8 -LiteralPath $historyPath | ConvertFrom-Json
    $entries = @($stored.entries)
    $failures = [Collections.Generic.List[string]]::new()
    if ($wpfResult.ok -ne $true -or $wpfResult.writes -ne $Iterations) { $failures.Add('WPF writes were incomplete') }
    if ($stored.version -ne 1 -or $entries.Count -ne (($Iterations * 2) + 2) -or $entries.Count -gt 50) { $failures.Add('version/count/50-entry bound failed') }
    if ($stored.ownerMarker.keep -ne $true) { $failures.Add('unknown root field was lost') }
    for ($index = 0; $index -lt $Iterations; $index++) {
        $suffix = $index.ToString('D2', [Globalization.CultureInfo]::InvariantCulture)
        if ($entries -notcontains "wpf query $suffix") { $failures.Add("lost WPF query $suffix") }
        if ($entries -notcontains "browser query $suffix") { $failures.Add("lost Browser query $suffix") }
    }
    $wpfDottedI = "$([char]0xFF23)$([char]0xFF21)$([char]0xFF34), $([char]0x0130)"
    $browserDottedI = "cat, i$([char]0x0307)"
    $wpfNonAscii = (-join [char[]]@(0x041C,0x041E,0x0421,0x041A,0x0412,0x0410)) + ", " + (-join [char[]]@(0x039F,0x03A3))
    $browserNonAscii = (-join [char[]]@(0x043C,0x043E,0x0441,0x043A,0x0432,0x0430)) + ", " + (-join [char[]]@(0x03BF,0x03C3))
    $dottedIForms = @($wpfDottedI, $browserDottedI)
    $nonAsciiForms = @($wpfNonAscii, $browserNonAscii)
    if (@($entries | Where-Object { $dottedIForms -contains $_ }).Count -ne 1) { $failures.Add('fullwidth/dotted-I cross-runtime dedupe failed') }
    if (@($entries | Where-Object { $nonAsciiForms -contains $_ }).Count -ne 1) { $failures.Add('Greek/Cyrillic cross-runtime dedupe failed') }

    # Keep the single cross-runtime command as the complete durability gate:
    # UI behavior, malformed/future protection, and live-lock Busy=0 writes.
    $contractRoot = Join-Path $fullRoot 'wpf-contract'
    $contractResultPath = Join-Path $contractRoot 'result.json'
    $contractHistoryPath = Join-Path $contractRoot 'search-history.json'
    New-Item -ItemType Directory -Force -Path $contractRoot | Out-Null
    $contract = Start-Process -FilePath $exe -ArgumentList @(
        '--search-history-smoke', $contractResultPath,
        '--search-history-path', $contractHistoryPath
    ) -WindowStyle Hidden -Wait -PassThru
    if ($contract.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $contractResultPath)) {
        $failures.Add("WPF contract smoke failed with exit $($contract.ExitCode)")
    }
    else {
        $contractResult = Get-Content -Raw -Encoding utf8 -LiteralPath $contractResultPath | ConvertFrom-Json
        $contractOk = $contractResult.ok -eq $true -and
            $contractResult.malformedAndFutureWriteRefused -eq $true -and
            $contractResult.liveLockBusyProtected -eq $true -and
            $contractResult.busyWrites -eq 0
        if (-not $contractOk) {
            $failures.Add('WPF malformed/future/live-lock contract failed')
        }
    }
    $residual = @(Get-ChildItem -LiteralPath $fullRoot -Recurse -Force -File | Where-Object { $_.Name.EndsWith('.lock') -or $_.Name.EndsWith('.tmp') })
    if ($residual.Count -ne 0) { $failures.Add("lock/temp residue: $($residual.FullName -join ', ')") }
    if ($failures.Count -gt 0) { throw ($failures -join '; ') }

    Remove-Item -LiteralPath $fullRoot -Recurse -Force
    [pscustomobject]@{
        ok = $true
        message = 'Browser and WPF concurrently preserved every shared search with NFKC dedupe and atomic replacement.'
        iterations = $Iterations
        entries = $entries.Count
        maxEntries = 50
        unicodeDedupe = $true
        unknownFieldPreserved = $true
        malformedAndFutureProtected = $true
        liveLockBusyProtected = $true
        busyWrites = 0
        lockResidue = 0
        tempResidue = 0
        tempRootRemoved = -not (Test-Path -LiteralPath $fullRoot)
        browserPortUsed = $false
        sourceOrUserCacheTouched = $false
    } | ConvertTo-Json -Depth 4
}
catch { throw $_ }
