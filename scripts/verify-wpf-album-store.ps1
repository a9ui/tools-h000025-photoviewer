$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj'
$watch = [Diagnostics.Stopwatch]::StartNew()

Push-Location $repoRoot
try {
    & dotnet build $projectPath -c Release --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "WPF Album store Release build failed with exit code $LASTEXITCODE"
    }

    & corepack pnpm vitest run `
        src/lib/albums.test.ts `
        src/app/api/albums/route.test.ts `
        src/lib/albums.crossRuntime.test.ts
    if ($LASTEXITCODE -ne 0) {
        throw "Browser/WPF Album store verifier failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
    $watch.Stop()
}

[pscustomobject]@{
    ok = $true
    releaseBuild = $true
    browserWpfInterleaving = $true
    malformedAndFutureProtected = $true
    revisionConflictProtected = $true
    unknownFieldsPreserved = $true
    lockAndTempResidueAbsent = $true
    elapsedMs = $watch.ElapsedMilliseconds
} | ConvertTo-Json
