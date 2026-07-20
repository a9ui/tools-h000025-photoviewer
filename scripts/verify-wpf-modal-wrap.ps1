param(
    [string]$Configuration = "Release",
    [string]$OutputPath = (Join-Path $env:TEMP "photoviewer-wpf-modal-wrap.json"),
    [string]$StatePath = (Join-Path $env:TEMP "photoviewer-wpf-modal-wrap-state.json")
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj"
$fixtureFolder = Join-Path $repoRoot "local-native\ui-mockup"
$exe = Join-Path $repoRoot "local-native\PhotoViewer.Wpf\bin\$Configuration\net8.0-windows\PhotoViewer.Wpf.exe"

dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

foreach ($path in @($OutputPath, $StatePath, "$StatePath.lock")) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

$process = Start-Process -FilePath $exe `
    -ArgumentList @(
        '--modal-nav-smoke', ('"{0}"' -f $OutputPath),
        '--state-path', ('"{0}"' -f $StatePath),
        '--folder', ('"{0}"' -f $fixtureFolder),
        '--query', 'wpf',
        '--select-index', '1'
    ) `
    -WindowStyle Hidden `
    -PassThru `
    -Wait

if (Test-Path -LiteralPath $OutputPath) {
    $result = Get-Content -Raw -LiteralPath $OutputPath | ConvertFrom-Json
    $result | ConvertTo-Json -Depth 8
    if ($result.ok -ne $true -or $result.wrappedLastToFirst -ne $true -or $result.wrappedFirstToLast -ne $true) {
        exit 1
    }
}
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
