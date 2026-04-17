#!/usr/bin/env pwsh

[CmdletBinding()]
param(
	[string]$ClientUrl = "http://localhost:5230",
	[string]$ApiUrl = "http://localhost:5231",
	[string]$TestFilter = "FullyQualifiedName~WileyWorkspacePanelRouteSmokeTests",
	[switch]$SkipPlaywrightInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$apiProjectPath = "WileyCoWeb.Api/WileyCoWeb.Api.csproj"
$clientProjectPath = "WileyCoWeb.csproj"
$e2eProjectPath = "tests/WileyCoWeb.E2ETests/WileyCoWeb.E2ETests.csproj"
$resultsDirectory = Join-Path $workspaceRoot "TestResults\debug-runtime"
$apiStdOutLog = Join-Path $resultsDirectory "api.stdout.log"
$apiStdErrLog = Join-Path $resultsDirectory "api.stderr.log"
$clientStdOutLog = Join-Path $resultsDirectory "client.stdout.log"
$clientStdErrLog = Join-Path $resultsDirectory "client.stderr.log"

function Write-LogTail {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Label,
		[Parameter(Mandatory = $true)]
		[string]$Path,
		[int]$LineCount = 60
	)

	Write-Host ""
	Write-Host "=== $Label ===" -ForegroundColor Yellow
	if (Test-Path $Path) {
		Get-Content -Path $Path -Tail $LineCount
		return
	}

	Write-Host "<log file not found>"
}

function Wait-ForUrl {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Url,
		[Parameter(Mandatory = $true)]
		[System.Diagnostics.Process]$Process,
		[int]$TimeoutSeconds = 120
	)

	$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
	while ([DateTimeOffset]::UtcNow -lt $deadline) {
		if ($Process.HasExited) {
			throw "Process exited before $Url became ready. Exit code: $($Process.ExitCode)"
		}

		try {
			$response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 5
			if ($null -ne $response.StatusCode -and $response.StatusCode -lt 500) {
				return
			}
		}
		catch {
		}

		Start-Sleep -Milliseconds 500
	}

	throw "Timed out waiting for $Url after $TimeoutSeconds seconds."
}

function Stop-ProcessTree {
	param(
		[System.Diagnostics.Process]$Process
	)

	if ($null -eq $Process) {
		return
	}

	try {
		if ($Process.HasExited) {
			return
		}
	}
	catch {
		return
	}

	if ($IsWindows) {
		& taskkill /PID $Process.Id /T /F | Out-Null
		return
	}

	Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
}

function Stop-ListeningPortProcess {
	param(
		[int]$Port
	)

	if (-not $IsWindows) {
		return
	}

	$connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
	foreach ($connection in $connections) {
		try {
			$process = Get-Process -Id $connection.OwningProcess -ErrorAction Stop
			Write-Host "Stopping PID $($process.Id) on localhost:$Port ..." -ForegroundColor Yellow
			Stop-ProcessTree -Process $process
		}
		catch {
		}
	}
}

function Start-LoggedDotnetProcess {
	param(
		[Parameter(Mandatory = $true)]
		[string[]]$Arguments,
		[Parameter(Mandatory = $true)]
		[string]$StdOutLog,
		[Parameter(Mandatory = $true)]
		[string]$StdErrLog
	)

	return Start-Process -FilePath "dotnet" -ArgumentList $Arguments -WorkingDirectory $workspaceRoot -RedirectStandardOutput $StdOutLog -RedirectStandardError $StdErrLog -PassThru
}

$apiProcess = $null
$clientProcess = $null

try {
	New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
	Remove-Item -Path @($apiStdOutLog, $apiStdErrLog, $clientStdOutLog, $clientStdErrLog) -Force -ErrorAction SilentlyContinue
	Stop-ListeningPortProcess -Port 5230
	Stop-ListeningPortProcess -Port 5231

	Write-Host "Building Playwright smoke tests..." -ForegroundColor Cyan
	& dotnet build $e2eProjectPath -c Debug --nologo --verbosity minimal
	if ($LASTEXITCODE -ne 0) {
		throw "Failed to build $e2eProjectPath."
	}

	if (-not $SkipPlaywrightInstall) {
		$playwrightScript = Join-Path $workspaceRoot "tests/WileyCoWeb.E2ETests/bin/Debug/net9.0/playwright.ps1"
		if (Test-Path $playwrightScript) {
			Write-Host "Ensuring Playwright Chromium is installed..." -ForegroundColor Cyan
			& pwsh -NoProfile -ExecutionPolicy Bypass -File $playwrightScript install chromium
			if ($LASTEXITCODE -ne 0) {
				throw "Failed to install Playwright Chromium for the E2E smoke tests."
			}
		}
	}

	Write-Host "Starting API at $ApiUrl ..." -ForegroundColor Cyan
	$apiProcess = Start-LoggedDotnetProcess -Arguments @(
		"run",
		"--project",
		$apiProjectPath,
		"--launch-profile",
		"WileyCoWeb.Api"
	) -StdOutLog $apiStdOutLog -StdErrLog $apiStdErrLog

	Wait-ForUrl -Url "$ApiUrl/health" -Process $apiProcess -TimeoutSeconds 120
	Write-Host "API is responding." -ForegroundColor Green

	Write-Host "Starting client at $ClientUrl ..." -ForegroundColor Cyan
	$clientProcess = Start-LoggedDotnetProcess -Arguments @(
		"run",
		"--project",
		$clientProjectPath,
		"--launch-profile",
		"http"
	) -StdOutLog $clientStdOutLog -StdErrLog $clientStdErrLog

	Wait-ForUrl -Url $ClientUrl -Process $clientProcess -TimeoutSeconds 180
	Write-Host "Client is responding." -ForegroundColor Green

	$env:WILEYCO_E2E_BASE_URL = $ClientUrl
	Write-Host "Running local panel route smoke tests..." -ForegroundColor Cyan
	& dotnet test $e2eProjectPath `
		-c Debug `
		--no-build `
		--filter $TestFilter `
		--logger "console;verbosity=detailed" `
		--logger "trx;LogFileName=debug-runtime-smoke.trx" `
		--results-directory $resultsDirectory

	if ($LASTEXITCODE -ne 0) {
		Write-Host "Smoke test execution failed. Recent runtime logs follow." -ForegroundColor Red
		Write-LogTail -Label "API stdout" -Path $apiStdOutLog
		Write-LogTail -Label "API stderr" -Path $apiStdErrLog
		Write-LogTail -Label "Client stdout" -Path $clientStdOutLog
		Write-LogTail -Label "Client stderr" -Path $clientStdErrLog
		exit $LASTEXITCODE
	}

	Write-Host "Smoke test passed. Logs and TRX output are in $resultsDirectory" -ForegroundColor Green
}
catch {
	Write-Error $_
	Write-LogTail -Label "API stdout" -Path $apiStdOutLog
	Write-LogTail -Label "API stderr" -Path $apiStdErrLog
	Write-LogTail -Label "Client stdout" -Path $clientStdOutLog
	Write-LogTail -Label "Client stderr" -Path $clientStdErrLog
	exit 1
}
finally {
	Stop-ProcessTree -Process $clientProcess
	Stop-ProcessTree -Process $apiProcess
	Remove-Item Env:WILEYCO_E2E_BASE_URL -ErrorAction SilentlyContinue
}