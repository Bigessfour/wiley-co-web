#!/usr/bin/env pwsh
<#
.SYNOPSIS
  One-command starter for fully local Windows Wiley Widget (Docker Postgres + API).

.EXAMPLE
  pwsh -NoProfile -File .\Scripts\start-wiley-widget-local.ps1 -StartClient
#>
[CmdletBinding()]
param(
    [string]$ApiProject = "WileyCoWeb.Api/WileyCoWeb.Api.csproj",
    [string]$ComposeFile = "docker-compose.yml",
    [int]$ApiPort = 5231,
    [int]$ClientPort = 5230,
    [switch]$NoDocker,
    [switch]$SkipMigrate,
    [switch]$StartClient,
    [switch]$SkipPortCleanup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:DockerPostgresConnection =
    "Host=localhost;Port=5433;Database=wileyco_local;Username=postgres;Password=localdevpw"

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

. (Join-Path $PSScriptRoot "LocalStartupLogging.ps1")
$logPath = Initialize-LocalStartupLog -RepoRoot $root -Prefix "local-start"
$script:LocalStartupLogPrefix = "local-start"

function Stop-WorkspaceDevPorts {
    foreach ($port in @($ClientPort, $ApiPort)) {
        Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue |
            ForEach-Object {
                Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue
            }
    }
}

function Test-HostPortConflict {
    param([int]$Port, [string[]]$ExpectedProcessNames)
    $listeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    foreach ($listener in $listeners) {
        $processName = (Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue).ProcessName
        if ($processName -and $ExpectedProcessNames -notcontains $processName) {
            return @{ Port = $Port; ProcessName = $processName; ProcessId = $listener.OwningProcess }
        }
    }
    return $null
}

function Repair-DevelopmentLocalSettings {
    $localPath = Join-Path $root "WileyCoWeb.Api/appsettings.Development.local.json"
    $examplePath = Join-Path $root "WileyCoWeb.Api/appsettings.Development.local.example.json"

    if (-not (Test-Path $localPath)) {
        if (Test-Path $examplePath) {
            Copy-Item -Path $examplePath -Destination $localPath
            Write-LocalStartupLog "Created appsettings.Development.local.json from example."
        }
        return
    }

    try {
        $json = Get-Content -Path $localPath -Raw | ConvertFrom-Json
        $connection = $json.ConnectionStrings.DefaultConnection
        $needsRepair = [string]::IsNullOrWhiteSpace($connection) -or $connection -match 'Port=5432;'
        if ($needsRepair) {
            if (-not $json.ConnectionStrings) {
                $json | Add-Member -NotePropertyName ConnectionStrings -NotePropertyValue ([pscustomobject]@{})
            }
            $json.ConnectionStrings.DefaultConnection = $script:DockerPostgresConnection
            if (-not $json.Database) {
                $json | Add-Member -NotePropertyName Database -NotePropertyValue ([pscustomobject]@{})
            }
            $json.Database.AllowDegradedStartup = $false
            $json.Database.SeedDevelopmentData = $true
            $json | ConvertTo-Json -Depth 6 | Set-Content -Path $localPath -Encoding utf8
            Write-LocalStartupLog "Repaired appsettings.Development.local.json (empty or Port=5432)."
        }
    } catch {
        Write-LocalStartupLog "Could not repair appsettings.Development.local.json: $($_.Exception.Message)" -Level WARN
    }
}

function Ensure-WorkspaceLocalSettings {
    $localPath = Join-Path $root "appsettings.Workspace.local.json"
    $examplePath = Join-Path $root "appsettings.Workspace.local.example.json"
    if (Test-Path $localPath) { return }
    if (-not (Test-Path $examplePath)) { return }
    Copy-Item -Path $examplePath -Destination $localPath
    Write-LocalStartupLog "Created appsettings.Workspace.local.json from example."
}

function Start-WorkspaceClientProcess {
    $clientScript = Join-Path $PSScriptRoot "start-wiley-widget-client.ps1"
    $proc = Start-Process pwsh -ArgumentList @(
        "-NoExit",
        "-NoProfile",
        "-File",
        $clientScript,
        "-ApiBaseAddress",
        "http://127.0.0.1:$ApiPort"
    ) -WorkingDirectory $root -PassThru
    Write-LocalStartupLog "Launched client window (PID $($proc.Id)). Watch that window for [local-client] logs."
    Write-LocalStartupLog "Client logs also under TestResults\local-startup\local-client-*.log"
}

try {
    Write-LocalStartupLog "Startup log: $logPath"
    Write-LocalStartupLog "Workspace URL (after client starts): http://127.0.0.1:${ClientPort}/wiley-workspace"
    Write-LocalStartupLog "API health URL: http://127.0.0.1:${ApiPort}/health"

    $sdk = dotnet --version
    Write-LocalStartupLog ".NET SDK: $sdk"

    if (-not $SkipPortCleanup) {
        Write-LocalStartupLog "Freeing ports $ClientPort and $ApiPort if occupied..."
        Stop-WorkspaceDevPorts
    }

    $pgConflict = Test-HostPortConflict -Port 5432 -ExpectedProcessNames @('postgres', 'wsl', 'com.docker.backend')
    if ($pgConflict) {
        Write-LocalStartupLog "Port 5432 in use by $($pgConflict.ProcessName) (PID $($pgConflict.ProcessId)). Docker uses host 5433." -Level WARN
    }

    Repair-DevelopmentLocalSettings
    Ensure-WorkspaceLocalSettings
    & (Join-Path $PSScriptRoot "Apply-SyncfusionLicenseEnv.ps1") -RepoRoot $root

    if ([string]::IsNullOrWhiteSpace($env:SYNCFUSION_LICENSE_KEY)) {
        Write-LocalStartupLog "SYNCFUSION_LICENSE_KEY not resolved." -Level WARN
    }

    if (-not $NoDocker) {
        Write-LocalStartupLog "Docker compose up ($ComposeFile)..." -Level STEP
        if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
            throw "docker not found. Install Docker Desktop or use -NoDocker."
        }
        docker compose -f $ComposeFile up -d 2>&1 | ForEach-Object { Write-LocalStartupLog $_ }

        $healthy = $false
        for ($i = 0; $i -lt 12; $i++) {
            $status = docker inspect -f '{{.State.Health.Status}}' wiley-widget-local-db 2>$null
            Write-LocalStartupLog "Postgres health check $($i + 1)/12: $status"
            if ($status -eq 'healthy') { $healthy = $true; break }
            Start-Sleep -Seconds 5
        }
        if (-not $healthy) {
            Write-LocalStartupLog "Postgres not healthy yet. Check: docker logs wiley-widget-local-db" -Level WARN
        }

        $env:ConnectionStrings__DefaultConnection = $script:DockerPostgresConnection
        $env:DATABASE_URL = $script:DockerPostgresConnection
        Write-LocalStartupLog "DATABASE_URL set (host port 5433)."
    }

    Invoke-DotNetLogged -Arguments @("build", $ApiProject, "-c", "Debug", "--nologo", "-v", "minimal") -StepName "API build"

    if (-not $SkipMigrate) {
        Write-LocalStartupLog "EF database update..." -Level STEP
        $migrateOutput = dotnet ef database update `
            --project src/WileyWidget.Data/WileyWidget.Data.csproj `
            --startup-project $ApiProject `
            --configuration Debug 2>&1
        $migrateOutput | ForEach-Object { Write-LocalStartupLog $_ }
        if ($LASTEXITCODE -ne 0) {
            Write-LocalStartupLog "EF migrate failed (exit $LASTEXITCODE)." -Level WARN
            if ($migrateOutput -match '28P01|password authentication failed') {
                Write-LocalStartupLog "Try: docker compose down -v; docker compose up -d" -Level WARN
            }
        } else {
            Write-LocalStartupLog "EF migrate succeeded."
        }
    }

    if ($StartClient) {
        Write-LocalStartupLog "Starting client in separate window (API must become healthy first in that window)." -Level STEP
        Start-WorkspaceClientProcess
    } else {
        Write-LocalStartupLog "Start client: pwsh -NoProfile -File .\Scripts\start-wiley-widget-client.ps1"
    }

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$ApiPort"
    Write-LocalStartupLog "Starting API: dotnet run --urls http://127.0.0.1:$ApiPort" -Level STEP

    & dotnet run --project $ApiProject --no-launch-profile --no-build --configuration Debug --urls "http://127.0.0.1:$ApiPort" 2>&1 | ForEach-Object {
        Write-Host $_
        if ($script:LocalStartupLogPath) {
            Add-Content -Path $script:LocalStartupLogPath -Value $_ -Encoding utf8
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "API dotnet run exited with code $LASTEXITCODE"
    }
} catch {
    Write-LocalStartupLog $_.Exception.Message -Level ERROR
    Test-LocalPortListening -Port $ApiPort -Label "API" | Out-Null
    Test-LocalPortListening -Port $ClientPort -Label "Client" | Out-Null
    Write-LocalStartupLog "Full log: $logPath" -Level WARN
    throw
} finally {
    Pop-Location
}
