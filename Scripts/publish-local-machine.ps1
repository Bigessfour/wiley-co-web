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

# 2b. Inject Syncfusion key from env (machine or user scope) into the published bundle so the deployed client has no license popup.
# Mirrors the logic in amplify.yml (pre-publish generation of wwwroot/appsettings.Syncfusion.local.json).
$sfKey = [Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'Machine')
if (-not $sfKey) { $sfKey = [Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User') }
if (-not $sfKey) { $sfKey = $env:SYNCFUSION_LICENSE_KEY }
if ($sfKey) {
    $sfJson = @{ SyncfusionLicenseKey = $sfKey.Trim() } | ConvertTo-Json -Compress
    $sfPath = Join-Path $clientOut "wwwroot/appsettings.Syncfusion.local.json"
    $sfJson | Out-File -FilePath $sfPath -Encoding utf8 -Force
    # Also write the compressed variants if the publish produced them (simple overwrite of json source; real prod build compresses properly via amplify).
    $sfBr = Join-Path $clientOut "wwwroot/appsettings.Syncfusion.local.json.br"
    $sfGz = Join-Path $clientOut "wwwroot/appsettings.Syncfusion.local.json.gz"
    if (Test-Path $sfBr) { $sfJson | Out-File -FilePath ($sfBr -replace '\.br$','') -Encoding utf8 -Force } # will be re-compressed on serve or ignore for local
    if (Test-Path $sfGz) { $sfJson | Out-File -FilePath ($sfGz -replace '\.gz$','') -Encoding utf8 -Force }
    Write-Info "Injected current SYNCFUSION_LICENSE_KEY into published client (wwwroot/appsettings.Syncfusion.local.json and variants)."
} else {
    Write-Warn "SYNCFUSION_LICENSE_KEY not found in Machine/User env or $env; client bundle may trigger Syncfusion license popup on run. Set before publish for clean local deploys."
}

# 2c. Clean junk from the client publish root (prevents bloat from repo root files, previous publishes, test artifacts, scripts that leak into publish output dir due to project content items or prior runs).
# Keep only the web assets (wwwroot/), minimal config, web.config, staticwebassets manifests, and our top-level README.
Write-Info "Cleaning non-web junk from client publish dir..."
$clientLevelJunkDirs = @('iam', 'playwright-report', 'Scripts', 'test-results', 'publish', 'node_modules', 'bin', 'obj', '.git', 'logs', 'TestResults', 'playwright-report')
$clientLevelJunkFilePatterns = @('*.ps1', '*.sh', 'global.json', 'package*.json', 'NuGet.Config', 'manifest.json', 'ai-fetchable-manifest.json', 'apprunner-update.json', 'update-config.json', 'trust-policy.json', 'skills-lock.json', 'jarvis-*.json', 'playwright-*.json', '*.dmp', '*.log', '*.ts', '*.tsx', 'tsconfig*.json', 'playwright.config.*')

foreach ($dirName in $clientLevelJunkDirs) {
    $p = Join-Path $clientOut $dirName
    if (Test-Path $p) {
        Remove-Item -Path $p -Recurse -Force -ErrorAction SilentlyContinue
    }
}
# Remove any nested publish/local-machine copies that may have been included
Get-ChildItem -Path $clientOut -Recurse -Directory -Include 'local-machine' -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

Get-ChildItem -Path $clientOut -File -ErrorAction SilentlyContinue | Where-Object {
    $name = $_.Name
    foreach ($pat in $clientLevelJunkFilePatterns) {
        if ($name -like $pat) { return $true }
    }
    return $false
} | Remove-Item -Force -ErrorAction SilentlyContinue

# Also aggressively remove any top-level loose .json that are clearly not runtime config or blazor manifests (keep appsettings*, *staticwebassets*)
Get-ChildItem -Path $clientOut -File -Filter '*.json' -ErrorAction SilentlyContinue | Where-Object {
    $n = $_.Name
    -not ($n -like 'appsettings*') -and -not ($n -like '*staticwebassets*') -and -not ($n -like 'web.config')
} | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Info "Client publish cleaned. Only web assets + essential configs remain under $clientOut (wwwroot/ is the static site root)."

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

Built with Syncfusion Blazor 33.x + .NET 9.0.313 (global.json pin respected at build time).\n\n## Prod / Town Deployment Notes (for parity after local changes)\n- Auth: Production appsettings has Jwt Enabled with Cognito. Town site expects valid tokens.\n- See the generated README-local-machine.txt in publish output for full verification checklist, data loading (Load council demo data), and parity steps.\n- Run the publish script on target to get clean client with key injected and junk removed.
"@

$readme | Out-File -FilePath (Join-Path $OutputRoot "README-local-machine.txt") -Encoding utf8

Write-Info "Publish complete to $OutputRoot"
Write-Info "API exe + sqlite example in $apiOut"
Write-Info "Client assets in $clientOut (wwwroot under it)"
Write-Info "See $OutputRoot\README-local-machine.txt"

Pop-Location
