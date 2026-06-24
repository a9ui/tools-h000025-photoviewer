param(
  [string]$ChatGptProjectUrl = "",
  [string]$DeploymentUrl = "",
  [string]$ProductionUrl = ""
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
chcp 65001 > $null

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $Root
try {
  $toml = Get-Content -Raw -Encoding UTF8 .\project.toml
  if ($ChatGptProjectUrl) {
    $toml = $toml -replace 'chatgpt_project = ".*"', ('chatgpt_project = "' + $ChatGptProjectUrl + '"')
  }
  if ($DeploymentUrl) {
    $toml = $toml -replace 'deployment = ".*"', ('deployment = "' + $DeploymentUrl + '"')
  }
  if ($ProductionUrl) {
    $toml = $toml -replace 'production = ".*"', ('production = "' + $ProductionUrl + '"')
  }
  $toml | Set-Content -Encoding UTF8 .\project.toml

  [pscustomobject]@{
    ok = $true
    chatgpt_project = $ChatGptProjectUrl
    deployment = $DeploymentUrl
    production = $ProductionUrl
  } | ConvertTo-Json -Depth 5
} finally {
  Pop-Location
}
