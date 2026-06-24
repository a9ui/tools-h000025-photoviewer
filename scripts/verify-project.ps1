$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$required = @("AGENTS.md", "PROJECT.md", "DESIGN.md", "project.toml")
$missing = foreach ($rel in $required) {
  if (-not (Test-Path -LiteralPath (Join-Path $root $rel))) { $rel }
}

$ok = $missing.Count -eq 0
[pscustomobject]@{
  ok = $ok
  missing = @($missing)
} | ConvertTo-Json -Depth 4

if (-not $ok) { exit 1 }
