param(
  [string]$Folder = "",
  [switch]$HeadlessScan
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$NativeProject = Join-Path $ProjectRoot "local-native\PhotoViewer.Native\PhotoViewer.Native.csproj"

if (-not (Test-Path -LiteralPath $NativeProject)) {
  throw "Native project not found: $NativeProject"
}

$arguments = @("run", "--project", $NativeProject)
if ($HeadlessScan) {
  if ($Folder.Trim().Length -eq 0) {
    throw "-HeadlessScan requires -Folder."
  }

  $arguments += "--"
  $arguments += "--headless-scan"
  $arguments += $Folder
} elseif ($Folder.Trim().Length -gt 0) {
  $arguments += "--"
  $arguments += $Folder
}

dotnet @arguments
