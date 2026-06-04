#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Resolves SYNCFUSION_LICENSE_KEY from Machine/User/Process and applies it for local dev.

.DESCRIPTION
  Syncfusion Blazor WASM (NuGet.org packages) requires SyncfusionLicenseProvider.RegisterLicense
  before AddSyncfusionBlazor. The key is injected at build into wwwroot/appsettings.Syncfusion.local.json
  and read at client startup (see ClientStartup.cs). This script mirrors API LicenseBootstrapper env order.

  https://blazor.syncfusion.com/documentation/getting-started/license-key/how-to-register-in-an-application
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$Quiet
)

Set-StrictMode -Version Latest

function Write-Status([string]$Message) {
    if (-not $Quiet) {
        Write-Host "[syncfusion-env] $Message" -ForegroundColor Cyan
    }
}

function Get-SyncfusionLicenseKeyFromScopedEnvironment {
    foreach ($target in @(
            [System.EnvironmentVariableTarget]::Machine,
            [System.EnvironmentVariableTarget]::User,
            [System.EnvironmentVariableTarget]::Process
        )) {
        $value = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', $target)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return @{
                Key = $value.Trim().Trim('"')
                Source = "env-$($target.ToString().ToLowerInvariant()):SYNCFUSION_LICENSE_KEY"
            }
        }
    }

    return $null
}

function Write-SyncfusionLocalSettingsFile {
    param(
        [string]$Path,
        [string]$LicenseKey
    )

    if ([string]::IsNullOrWhiteSpace($LicenseKey)) {
        return
    }

    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $payload = @{ SyncfusionLicenseKey = $LicenseKey } | ConvertTo-Json -Compress
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $payload, $utf8NoBom)
}

$resolved = Get-SyncfusionLicenseKeyFromScopedEnvironment
if (-not $resolved) {
    if (-not $Quiet) {
        Write-Warning "[syncfusion-env] SYNCFUSION_LICENSE_KEY not found on Machine, User, or Process. Set it in Windows environment variables or appsettings.Syncfusion.local.json."
    }
    exit 0
}

$env:SYNCFUSION_LICENSE_KEY = $resolved.Key
Write-Status "Using $($resolved.Source) (length $($resolved.Key.Length))."

$rootFiles = @(
    (Join-Path $RepoRoot 'appsettings.Syncfusion.local.json'),
    (Join-Path $RepoRoot 'wwwroot\appsettings.Syncfusion.local.json')
)

foreach ($path in $rootFiles) {
    Write-SyncfusionLocalSettingsFile -Path $path -LicenseKey $resolved.Key
    Write-Status "Wrote $path"
}

if (-not $Quiet) {
    Write-Status 'Client build/run will register the license via wwwroot/appsettings.Syncfusion.local.json + SyncfusionLicenseProvider.RegisterLicense.'
}
