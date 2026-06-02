#!/usr/bin/env pwsh
<#
.SYNOPSIS
  One-command starter for fully local Windows 11 Wiley Widget (no AWS).
  Starts (or ensures) local Postgres via docker compose, runs the API, and notes client start.

.DESCRIPTION
  - Assumes Docker Desktop is installed and running (recommended path for DB fidelity).
  - If no Docker, user must have native Postgres running with matching conn string.
  - Uses existing dev fallbacks (degraded mode, seeds) if DB not ready.
  - Expects XAI_API_KEY (and optionally SYNCFUSION_LICENSE_KEY) in machine/user env.
  - Does NOT auto-launch browser or client; use separate terminal or VS launch for client on 5230.

  After API is up: open another terminal and run `dotnet run` (from repo root) or use the VS "Debug Full Stack" launch config.
  Workspace will be at http://localhost:5230 (API at 5231).

.EXAMPLE
  pwsh -NoProfile -File Scripts/start-wiley-widget-local.ps1
#>
[CmdletBinding()]
param(
    [string]$ApiProject = "WileyCoWeb.Api/WileyCoWeb.Api.csproj",
    [string]$ComposeFile = "docker-compose.yml",
    [int]$ApiPort = 5231,
    [switch]$NoDocker,
    [switch]$SkipMigrate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

function Write-Info($msg) { Write-Host "[local-start] $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Warning "[local-start] $msg" }
function Write-Err($msg) { Write-Error "[local-start] $msg" }

# 1. Basic .NET check
try {
    $sdk = dotnet --version
    Write-Info "Detected .NET SDK: $sdk (global.json pins 9.0.313; ensure match for builds)"
} catch {
    Write-Err "dotnet not found in PATH. Install .NET 9 SDK 9.0.313+."
    exit 1
}

# 2. Secrets reminder
$xai = [Environment]::GetEnvironmentVariable("XAI_API_KEY", "User") ?? [Environment]::GetEnvironmentVariable("XAI_API_KEY", "Machine")
if ([string]::IsNullOrWhiteSpace($xai)) {
    Write-Warn "XAI_API_KEY not found in User or Machine env. Jarvis will use fallbacks only. Set it for full AI (e.g. [Environment]::SetEnvironmentVariable('XAI_API_KEY','xai-...','User'))."
}

# 3. DB
if (-not $NoDocker) {
    Write-Info "Ensuring local Postgres via docker compose ($ComposeFile)..."
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Err "docker not found. Install Docker Desktop for Windows, or rerun with -NoDocker and provide your own Postgres on the connection string."
        exit 1
    }
    docker compose -f $ComposeFile up -d | Out-Null
    Write-Info "docker compose up -d complete. Waiting for healthy DB (up to ~60s)..."

    $healthy = $false
    for ($i = 0; $i -lt 12; $i++) {
        $status = docker inspect -f '{{.State.Health.Status}}' wiley-widget-local-db 2>$null
        if ($status -eq 'healthy') { $healthy = $true; break }
        Start-Sleep -Seconds 5
    }
    if (-not $healthy) {
        Write-Warn "DB container not reporting healthy yet. Startup will attempt degraded mode or you can wait and rerun. Check 'docker logs wiley-widget-local-db'."
    } else {
        Write-Info "Postgres is healthy."
    }

    if (-not $SkipMigrate) {
        Write-Info "Applying EF migrations (idempotent)..."
        try {
            dotnet ef database update --project src/WileyWidget.Data/WileyWidget.Data.csproj --startup-project $ApiProject --no-build 2>&1 | Out-Null
            Write-Info "Migrations applied (or up-to-date)."
        } catch {
            Write-Warn "EF migrate step had issues (may be first run or build needed). API startup will handle EnsureCreated/seed where configured."
        }
    }
} else {
    Write-Info "-NoDocker: assuming you have Postgres running with your configured connection string (DefaultConnection or DATABASE_URL)."
}

# 4. Build API (quick) then run
Write-Info "Building API (Debug)..."
dotnet build $ApiProject -c Debug --nologo -v q | Out-Null

Write-Info "Starting API host on port $ApiPort (Development, will respect appsettings.Development.local.json + env). Ctrl+C to stop."
Write-Info "Client: in another terminal run 'dotnet run' from repo root (or use VS 'Debug Full Stack')."
Write-Info "Workspace: http://localhost:5230  |  API health: http://localhost:$ApiPort/health"

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:$ApiPort"

# Run (foreground; user stops with Ctrl+C)
dotnet run --project $ApiProject --no-launch-profile --no-build --configuration Debug --urls "http://localhost:$ApiPort"

Pop-Location
