#!/usr/bin/env pwsh

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$targets = @(
    [PSCustomObject]@{ Path = Join-Path $workspaceRoot "logs\wiley-widget.log"; Kind = "file" },
    [PSCustomObject]@{ Path = Join-Path $workspaceRoot "test-results"; Kind = "directory" },
    [PSCustomObject]@{ Path = Join-Path $workspaceRoot "TestResults"; Kind = "directory" }
)

$removed = @()
$missing = @()

foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target.Path)) {
        $missing += $target.Path
        continue
    }

    if ($target.Kind -eq "file") {
        Remove-Item -LiteralPath $target.Path -Force
        $removed += $target.Path
        continue
    }

    Get-ChildItem -LiteralPath $target.Path -Force -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }

    $removed += "$($target.Path)\*"
}

if ($removed.Count -gt 0) {
    Write-Host "Cleared log and result artifacts:" -ForegroundColor Green
    $removed | ForEach-Object { Write-Host "  - $_" }
}
else {
    Write-Host "No log or result artifacts needed clearing." -ForegroundColor Yellow
}

if ($missing.Count -gt 0) {
    Write-Host "Skipped missing targets:" -ForegroundColor DarkYellow
    $missing | ForEach-Object { Write-Host "  - $_" }
}