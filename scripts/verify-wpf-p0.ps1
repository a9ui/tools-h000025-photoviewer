param(
    [string]$OutputPath = (Join-Path $env:TEMP "photoviewer-wpf-p0d-gate.json")
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj'
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $root 'local-native\PhotoViewer.Wpf\bin\Release\net8.0-windows\PhotoViewer.Wpf.exe'
Remove-Item -LiteralPath $OutputPath -ErrorAction SilentlyContinue
& $exe --p0d-smoke $OutputPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$result = Get-Content -Raw -LiteralPath $OutputPath | ConvertFrom-Json
if (-not $result.ok) { throw "WPF P0D gate did not pass: $($result.message)" }
$result
