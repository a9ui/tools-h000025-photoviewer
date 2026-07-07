param(
  [string]$Folder = "",
  [switch]$HeadlessScan,
  [switch]$HeadlessImport,
  [switch]$HeadlessSearch,
  [string]$Search = "",
  [switch]$FavoritesOnly,
  [string]$FavoritePath = "",
  [int]$FavoriteLevel = 0
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
