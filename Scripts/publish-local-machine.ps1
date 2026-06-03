#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Publish self-contained Windows x64 binaries for local machine "computer hosted" deployment (no SDK, no Docker required at runtime for SQLite mode).

.DESCRIPTION
  - Builds Release, self-contained for win-x64.
  - API: single-file exe preferred for simple deploy (wiley-widget-api.exe + deps if not single).
  - Client: standard Blazor WASM publish (static files in wwwroot; can be served by any static host or copied into API for optional same-origin mode later).
  - Outputs to ./publish/local-machine/ with README for run instructions.
  - For pure SQLite zero-dep: user sets Database:Provider=SQLite + Data Source=%LOCALAPPDATA%\... in config or env.
  - Includes the sqlite example config copied for convenience.
  - Run from repo root: pwsh -File Scripts/publish-local-machine.ps1

  After publish, copy the output folder to target Win11 machine, set XAI_API_KEY, run the API exe (or use start script), open client in browser pointed at API.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "publish/local-machine",
    [switch]$SingleFile = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

function Write-Info($msg) { Write-Host "[publish-local] $msg" -ForegroundColor Cyan }

Write-Info "Starting local machine self-contained publish (SQLite-friendly, no runtime SDK needed on target)..."

# Clean previous
if (Test-Path $OutputRoot) {
    Write-Info "Cleaning previous publish at $OutputRoot"
    Remove-Item -Recurse -Force $OutputRoot -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

$apiOut = Join-Path $OutputRoot "api"
$clientOut = Join-Path $OutputRoot "client"
New-Item -ItemType Directory -Path $apiOut -Force | Out-Null
New-Item -ItemType Directory -Path $clientOut -Force | Out-Null

# 1. Publish API (self-contained)
$apiProject = "WileyCoWeb.Api/WileyCoWeb.Api.csproj"
$publishArgs = @(
    "publish", $apiProject,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-o", $apiOut
)
if ($SingleFile) {
    $publishArgs += "-p:PublishSingleFile=true"
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
}
Write-Info "Publishing API self-contained to $apiOut ..."
dotnet @publishArgs | Out-Null
if ($LASTEXITCODE -ne 0) { throw "API publish failed" }

# Copy sqlite example for easy start on target machine
$exampleSrc = "WileyCoWeb.Api/appsettings.Development.sqlite.example.json"
if (Test-Path $exampleSrc) {
    Copy-Item $exampleSrc (Join-Path $apiOut "appsettings.Development.sqlite.example.json") -Force
    Write-Info "Copied sqlite example config to API publish root (rename to .local.json or use env on target)"
}

# 2. Publish Client (WASM static assets)
$clientProject = "WileyCoWeb.csproj"
Write-Info "Publishing client WASM assets to $clientOut ..."
dotnet publish $clientProject -c $Configuration -o $clientOut | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Client publish failed" }

# 3. Basic README for the publish output
$readme = @"
# Wiley Widget - Local Machine Publish (Self-Contained, Windows x64)

Generated on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Runtime: $Runtime | Config: $Configuration | SingleFile: $SingleFile

## Quick Start on Target Windows 11 Machine (no .NET SDK required)

1. Copy this entire folder to the target machine (e.g. C:\WileyWidget\).

2. Set required secrets (PowerShell as admin or current user):
   [Environment]::SetEnvironmentVariable("XAI_API_KEY", "xai-your-key-here", "User")
   # Optional for full Syncfusion:
   # [Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "your-key", "User")

3. For pure zero-dep SQLite mode (recommended for cost-free local):
   - Copy/rename `appsettings.Development.sqlite.example.json` (in the api/ folder) to `appsettings.Development.local.json` (gitignored pattern).
   - Edit the local file if needed: ensure "Database": { "Provider": "SQLite", "SqliteDataPath": "%LOCALAPPDATA%\\WileyWidget\\wiley-widget.db" }
   - The DB file + logs will be created under %LOCALAPPDATA%\WileyWidget\ on first run.

4. Run the API (self-contained exe):
   cd api
   .\WileyCoWeb.Api.exe   # or the single-file name if published that way
   # API will be on http://localhost:5231 by default (or configured).

5. Run the client (in another terminal or as static site):
   cd ..
   # Simplest: use any static server on the client wwwroot, or point browser to a hosted version of the client files configured to call your local API.
   # For dev/test on same machine: from repo (if SDK present) or serve the client/wwwroot folder on port 5230 with API base set via env or local settings.
   # Example with simple server (if node available): npx serve client/wwwroot -l 5230
   # Then open http://localhost:5230 and ensure WILEY_WORKSPACE_API_BASE_ADDRESS=http://localhost:5231 (or use the launch settings).

6. Open browser to client (http://localhost:5230). Use the workspace, import from Import Data/ folder samples, test Jarvis (needs valid xAI key), export, etc.

## Notes
- This is for the "computer hosted process" / cost-savings migration. Default remains PostgreSQL for fidelity/CI.
- Use the sqlite example + EncryptedLocalSecretVaultService (DPAPI) or env for secrets on the machine.
- For single-process feel later: implement UseStaticFiles + fallback in API to serve client wwwroot (same-origin).
- Verify with: after first run, check %LOCALAPPDATA%\WileyWidget\wiley-widget.db exists (for SQLite).
- Full packaging + launcher + Windows Service (UseWindowsService) is next iteration.
- See root README.md "Local Windows Machine / AWS-Decoupled Operation" and .grok/prompts/db-multi-provider-sqlite-feasibility.md for status + manual smoke evidence.

Built with Syncfusion Blazor + .NET 9 (global.json pin respected at build time).
"@

$readme | Out-File -FilePath (Join-Path $OutputRoot "README-local-machine.txt") -Encoding utf8

Write-Info "Publish complete to $OutputRoot"
Write-Info "API exe + sqlite example in $apiOut"
Write-Info "Client assets in $clientOut (wwwroot under it)"
Write-Info "See $OutputRoot\README-local-machine.txt"

Pop-Location
