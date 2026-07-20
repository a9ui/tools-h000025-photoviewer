param(
    [string]$Configuration = 'Release',
    [string]$EvidenceDir = (Join-Path ([IO.Path]::GetTempPath()) ('photoviewer-wpf-visual-layout-' + [guid]::NewGuid().ToString('N'))),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj'
$exe = Join-Path $repoRoot "local-native\PhotoViewer.Wpf\bin\$Configuration\net8.0-windows\PhotoViewer.Wpf.exe"
$fixture = Join-Path $repoRoot 'local-native\ui-mockup'

if (-not $SkipBuild) {
    dotnet build $project -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if (-not (Test-Path -LiteralPath $exe)) { throw "WPF executable was not found: $exe" }
if (-not (Test-Path -LiteralPath $fixture)) { throw "Visual fixture was not found: $fixture" }

New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
$states = @(
    @{ Name = 'landing'; Args = @('--screen', 'landing', '--folder', $fixture) },
    @{ Name = 'viewer'; Args = @('--screen', 'viewer', '--folder', $fixture, '--clear-selection') },
    @{ Name = 'settings'; Args = @('--screen', 'viewer', '--folder', $fixture, '--clear-selection', '--show-unseen-dots', '--show-app-settings') },
    @{ Name = 'folders-collapsed'; Args = @('--screen', 'viewer', '--folder', $fixture, '--clear-selection', '--folders-collapsed') },
    @{ Name = 'unseen-on'; Args = @('--screen', 'viewer', '--folder', $fixture, '--clear-selection', '--show-unseen-dots') }
)
$sizes = @(
    @{ Width = 1280; Height = 820 },
    @{ Width = 1024; Height = 700 }
)
$outputs = [Collections.Generic.List[object]]::new()

Add-Type -AssemblyName PresentationCore
foreach ($size in $sizes) {
    foreach ($state in $states) {
        $fileName = 'wpf-final-{0}-{1}x{2}.png' -f $state.Name, $size.Width, $size.Height
        $output = Join-Path $EvidenceDir $fileName
        Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue
        $arguments = @('--shot', $output, '--shot-width', [string]$size.Width, '--shot-height', [string]$size.Height) + $state.Args
        $quoted = @($arguments | ForEach-Object { '"{0}"' -f ($_ -replace '"', '\"') })
        $process = Start-Process -FilePath $exe -ArgumentList $quoted -WindowStyle Hidden -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw "$fileName exited with $($process.ExitCode)" }
        if (-not (Test-Path -LiteralPath $output)) { throw "$fileName was not created" }

        $stream = [IO.File]::OpenRead($output)
        try {
            $decoder = [Windows.Media.Imaging.PngBitmapDecoder]::new(
                $stream,
                [Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
                [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
            $frame = $decoder.Frames[0]
            if ($frame.PixelWidth -ne $size.Width -or $frame.PixelHeight -ne $size.Height) {
                throw "$fileName has $($frame.PixelWidth)x$($frame.PixelHeight); expected $($size.Width)x$($size.Height)"
            }
        }
        finally {
            $stream.Dispose()
        }
        if ((Get-Item -LiteralPath $output).Length -lt 10000) { throw "$fileName appears blank or incomplete" }
        $outputs.Add([pscustomobject]@{
            state = $state.Name
            width = $size.Width
            height = $size.Height
            path = $output
            bytes = (Get-Item -LiteralPath $output).Length
            sha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
        })
    }
}

[pscustomobject]@{
    ok = $true
    message = 'WPF visual reference states rendered at both supported audit sizes'
    fixture = $fixture
    evidenceDir = $EvidenceDir
    outputs = $outputs
} | ConvertTo-Json -Depth 5
