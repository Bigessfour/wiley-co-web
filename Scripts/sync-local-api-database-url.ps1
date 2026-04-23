#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [string]$Region = "us-east-2",
    [string]$SecretName = "wiley-widget/api/database-url",
    [string]$Project = "WileyCoWeb.Api/WileyCoWeb.Api.csproj",
    [string]$LocalSettingsPath = "WileyCoWeb.Api/appsettings.Development.local.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-DatabaseUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SecretString
    )

    try {
        $parsed = $SecretString | ConvertFrom-Json -ErrorAction Stop
        foreach ($propertyName in @(
            "DATABASE_URL",
            "ConnectionStrings__DefaultConnection",
            "DefaultConnection",
            "defaultConnection",
            "databaseUrl")) {
            if ($parsed.PSObject.Properties.Name -contains $propertyName) {
                $value = [string]$parsed.$propertyName
                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    return $value
                }
            }
        }
    }
    catch {
    }

    return $SecretString
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

Push-Location $repoRoot
try {
    Write-Host "Fetching Aurora database URL from Secrets Manager secret '$SecretName' in region '$Region'."
    $secretString = aws secretsmanager get-secret-value --secret-id $SecretName --region $Region --query SecretString --output text
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($secretString)) {
        throw "Unable to retrieve a database URL from Secrets Manager secret '$SecretName'."
    }

    $databaseUrl = Resolve-DatabaseUrl -SecretString $secretString
    if ([string]::IsNullOrWhiteSpace($databaseUrl)) {
        throw "The retrieved secret did not contain a usable database connection string."
    }

    $localSettingsFullPath = Join-Path $repoRoot $LocalSettingsPath
    $localSettingsDirectory = Split-Path -Parent $localSettingsFullPath
    if (-not [string]::IsNullOrWhiteSpace($localSettingsDirectory) -and -not (Test-Path $localSettingsDirectory)) {
        New-Item -ItemType Directory -Path $localSettingsDirectory -Force | Out-Null
    }

    $localSettings = @{
        ConnectionStrings = @{
            DefaultConnection = $databaseUrl
        }
        Database = @{
            AllowDegradedStartup = $false
            SeedDevelopmentData = $false
        }
    } | ConvertTo-Json -Depth 4

    Set-Content -Path $localSettingsFullPath -Value $localSettings -Encoding UTF8

    & dotnet user-secrets set --project $Project "ConnectionStrings:DefaultConnection" $databaseUrl
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet user-secrets set failed with exit code $LASTEXITCODE."
    }

    Write-Host "Wrote local API connection override to $LocalSettingsPath." -ForegroundColor Green
    Write-Host "Stored ConnectionStrings:DefaultConnection in API user secrets for $Project." -ForegroundColor Green
    Write-Host "Next step: start the API host and verify the configured PostgreSQL endpoint is reachable from this machine." -ForegroundColor Green
}
finally {
    Pop-Location
}