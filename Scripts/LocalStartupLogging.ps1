#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Shared timestamped logging helpers for local Wiley Widget startup scripts.
#>
Set-StrictMode -Version Latest

function Initialize-LocalStartupLog {
    param(
        [string]$RepoRoot,
        [string]$Prefix = "startup"
    )

    $logDir = Join-Path $RepoRoot "TestResults\local-startup"
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $logPath = Join-Path $logDir "$Prefix-$timestamp.log"

    $script:LocalStartupLogPath = $logPath
    $script:LocalStartupLogPrefix = $Prefix

    Write-LocalStartupLog "Log file: $logPath"
    return $logPath
}

function Write-LocalStartupLog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [ValidateSet('INFO', 'WARN', 'ERROR', 'STEP')]
        [string]$Level = 'INFO'
    )

    $prefix = $script:LocalStartupLogPrefix ?? 'startup'
    $line = "[{0}] [{1}] {2}" -f (Get-Date -Format "HH:mm:ss.fff"), $Level, $Message
    $color = switch ($Level) {
        'WARN' { 'Yellow' }
        'ERROR' { 'Red' }
        'STEP' { 'Green' }
        default { 'Cyan' }
    }

    Write-Host "[$prefix] $Message" -ForegroundColor $color
    if ($script:LocalStartupLogPath) {
        Add-Content -Path $script:LocalStartupLogPath -Value $line -Encoding utf8
    }
}

function Invoke-DotNetLogged {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [string]$StepName,
        [string]$WorkingDirectory = (Get-Location).Path
    )

    Write-LocalStartupLog "dotnet $($Arguments -join ' ')" -Level STEP
    Push-Location $WorkingDirectory
    try {
        $output = & dotnet @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        foreach ($line in $output) {
            if ($script:LocalStartupLogPath) {
                Add-Content -Path $script:LocalStartupLogPath -Value $line -Encoding utf8
            }
        }

        if ($exitCode -ne 0) {
            Write-LocalStartupLog "$StepName failed (exit $exitCode). Last output:" -Level ERROR
            ($output | Select-Object -Last 30) | ForEach-Object {
                Write-Host $_ -ForegroundColor Red
                if ($script:LocalStartupLogPath) {
                    Add-Content -Path $script:LocalStartupLogPath -Value $_ -Encoding utf8
                }
            }
            throw "$StepName failed with exit code $exitCode. See log: $($script:LocalStartupLogPath)"
        }

        Write-LocalStartupLog "$StepName succeeded."
        return $output
    } finally {
        Pop-Location
    }
}

function Test-LocalPortListening {
    param(
        [int]$Port,
        [string]$Label = "port $Port"
    )

    $listeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $listeners) {
        Write-LocalStartupLog "$Label is not listening." -Level WARN
        return $false
    }

    $details = $listeners |
        Select-Object -First 3 LocalAddress, OwningProcess |
        ForEach-Object {
            $name = (Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName
            "$($_.LocalAddress):$Port (PID $($_.OwningProcess) $name)"
        }
    Write-LocalStartupLog "$Label listening: $($details -join '; ')"
    return $true
}

function Wait-LocalUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [int]$TimeoutSeconds = 120,
        [int]$ProgressIntervalSeconds = 10
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastProgress = [DateTime]::MinValue
    $lastError = $null
    $attempt = 0

    Write-LocalStartupLog "Waiting for $Url (timeout ${TimeoutSeconds}s)..." -Level STEP

    while ((Get-Date) -lt $deadline) {
        $attempt++
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                Write-LocalStartupLog "$Url ready (HTTP $($response.StatusCode), attempt $attempt)."
                return $true
            }
            $lastError = "HTTP $($response.StatusCode)"
        } catch {
            $lastError = $_.Exception.Message
        }

        if (((Get-Date) - $lastProgress).TotalSeconds -ge $ProgressIntervalSeconds) {
            Write-LocalStartupLog "Still waiting for $Url (attempt $attempt). Last error: $lastError" -Level WARN
            $lastProgress = Get-Date
        }

        Start-Sleep -Seconds 2
    }

    Write-LocalStartupLog "Timed out waiting for $Url. Last error: $lastError" -Level ERROR
    return $false
}
