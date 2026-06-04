#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Starts the Blazor dev client (port 5230) against a local API on 5231.

.EXAMPLE
  pwsh -NoProfile -File .\Scripts\start-wiley-widget-client.ps1
#>
[CmdletBinding()]
param(
    [string]$ClientProject = "WileyCoWeb.csproj",
    [string]$ApiBaseAddress = "http://127.0.0.1:5231",
    [int]$ClientPort = 5230,
    [int]$HealthWaitSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

. (Join-Path $PSScriptRoot "LocalStartupLogging.ps1")
$logPath = Initialize-LocalStartupLog -RepoRoot $root -Prefix "local-client"
$script:LocalStartupLogPrefix = "local-client"
$healthUrl = "{0}/health" -f $ApiBaseAddress.TrimEnd('/')
$workspaceUrl = "http://127.0.0.1:${ClientPort}/wiley-workspace"

try {
    & (Join-Path $PSScriptRoot "Apply-SyncfusionLicenseEnv.ps1") -RepoRoot $root

    Write-LocalStartupLog "ApiBaseAddress=$ApiBaseAddress"
    Write-LocalStartupLog "HealthUrl=$healthUrl"
    Write-LocalStartupLog "WorkspaceUrl=$workspaceUrl"
    Write-LocalStartupLog "Log=$logPath"

    if (-not (Wait-LocalUrl -Url $healthUrl -TimeoutSeconds $HealthWaitSeconds)) {
        throw "API not reachable at $healthUrl. Start API first: pwsh -NoProfile -File .\Scripts\start-wiley-widget-local.ps1"
    }

    Invoke-DotNetLogged -Arguments @("build", $ClientProject, "-c", "Debug", "--nologo", "-v", "minimal") -StepName "Client build"

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:WILEY_WORKSPACE_API_BASE_ADDRESS = $ApiBaseAddress

    Write-LocalStartupLog "Starting: dotnet run --project $ClientProject -c Debug --launch-profile http --no-build" -Level STEP
    Write-LocalStartupLog "When ready, open: $workspaceUrl"

    $runArgs = @(
        "run",
        "--project", $ClientProject,
        "-c", "Debug",
        "--launch-profile", "http",
        "--no-build"
    )

    # Run in foreground; tee output to log. User sees live output in this window.
    & dotnet @runArgs 2>&1 | ForEach-Object {
        Write-Host $_
        if ($script:LocalStartupLogPath) {
            Add-Content -Path $script:LocalStartupLogPath -Value $_ -Encoding utf8
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run exited with code $LASTEXITCODE. Log: $logPath"
    }
} catch {
    Write-LocalStartupLog $_.Exception.Message -Level ERROR
    if ($_.ScriptStackTrace) {
        Write-LocalStartupLog $_.ScriptStackTrace -Level ERROR
    }
    Test-LocalPortListening -Port $ClientPort -Label "Client port" | Out-Null
    Write-LocalStartupLog "Troubleshooting: confirm API health at $healthUrl and retry. Log: $logPath" -Level WARN
    throw
} finally {
    Pop-Location
}
