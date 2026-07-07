param(
  [string]$Folder = "",
  [switch]$HeadlessScan,
  [switch]$HeadlessIncrementalScan,
  [switch]$HeadlessImport,
  [switch]$HeadlessSearch,
  [switch]$HeadlessPerf,
  [string]$Search = "",
  [switch]$FavoritesOnly,
  [string]$FavoritePath = "",
  [int]$FavoriteLevel = 0,
  [int]$PerfIterations = 40
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
} elseif ($HeadlessIncrementalScan) {
  if ($Folder.Trim().Length -eq 0) {
    throw "-HeadlessIncrementalScan requires -Folder."
  }

  $arguments += "--"
  $arguments += "--headless-scan"
  $arguments += $Folder
  $arguments += "--incremental"
} elseif ($HeadlessImport) {
  $arguments += "--"
  $arguments += "--headless-import"
} elseif ($HeadlessSearch) {
  if ($Folder.Trim().Length -eq 0) {
    throw "-HeadlessSearch requires -Folder."
  }

  $arguments += "--"
  $arguments += "--headless-search"
  $arguments += $Folder
  $arguments += $Search
  if ($FavoritesOnly) {
    $arguments += "--favorites-only"
  }
} elseif ($HeadlessPerf) {
  if ($Folder.Trim().Length -eq 0) {
    throw "-HeadlessPerf requires -Folder."
  }

  $arguments += "--"
  $arguments += "--headless-perf"
  $arguments += $Folder
  $arguments += "--iterations"
  $arguments += $PerfIterations.ToString()
  if ($Search.Trim().Length -gt 0) {
    $arguments += "--search"
    $arguments += $Search
  }
} elseif ($FavoritePath.Trim().Length -gt 0) {
  $arguments += "--"
  $arguments += "--headless-favorite"
  $arguments += $FavoritePath
  $arguments += $FavoriteLevel.ToString()
} elseif ($Folder.Trim().Length -gt 0) {
  $arguments += "--"
  $arguments += $Folder
}

dotnet @arguments
