[CmdletBinding()]
param(
    [string]$Region = "us-east-2",
    [string]$SecretName = "wiley-widget/api/database-url",
    [string]$Project = "src/WileyWidget.Data/WileyWidget.Data.csproj",
    [string]$StartupProject = "WileyCoWeb.Api/WileyCoWeb.Api.csproj",
    [string]$Context = "AppDbContext",
    [string]$TargetMigration = "InitialCreate",
    [switch]$ResetToZero,
    [switch]$NoBuild
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

function Resolve-DatabaseEndpoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabaseUrl
    )

    if ($DatabaseUrl -match '^[a-zA-Z][a-zA-Z0-9+.-]*://') {
        $uri = [Uri]$DatabaseUrl
        return [pscustomobject]@{
            Host = $uri.Host
            Port = if ($uri.Port -gt 0) { $uri.Port } else { 5432 }
        }
    }

    $segments = @{}
    foreach ($segment in $DatabaseUrl.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $parts = $segment.Split('=', 2)
        if ($parts.Count -eq 2) {
            $segments[$parts[0].Trim()] = $parts[1].Trim()
        }
    }

    $endpointHost = $segments['Host']
    if ([string]::IsNullOrWhiteSpace($endpointHost)) {
        $endpointHost = $segments['Server']
    }
    if ([string]::IsNullOrWhiteSpace($endpointHost)) {
        $endpointHost = $segments['Data Source']
    }

    if ([string]::IsNullOrWhiteSpace($endpointHost)) {
        return $null
    }

    $port = 5432
    if ($segments.ContainsKey('Port')) {
        [void][int]::TryParse($segments['Port'], [ref]$port)
    }

    return [pscustomobject]@{
        Host = $endpointHost
        Port = $port
    }
}

function Test-TcpConnectivity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EndpointHost,
        [Parameter(Mandatory = $true)]
        [int]$Port,
        [int]$TimeoutMilliseconds = 5000
    )

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $asyncResult = $client.BeginConnect($EndpointHost, $Port, $null, $null)
        if (-not $asyncResult.AsyncWaitHandle.WaitOne($TimeoutMilliseconds, $false)) {
            return $false
        }

        $client.EndConnect($asyncResult)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Invoke-DotnetEfDatabaseUpdate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Migration
    )

    $arguments = @(
        "ef",
        "database",
        "update",
        $Migration,
        "--project", $Project,
        "--startup-project", $StartupProject,
        "--context", $Context
    )

    if ($NoBuild) {
        $arguments += "--no-build"
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef database update $Migration failed with exit code $LASTEXITCODE."
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$previousDatabaseUrl = $env:DATABASE_URL

Push-Location $repoRoot
try {
    Write-Host "Fetching Aurora database URL from Secrets Manager secret '$SecretName' in region '$Region'."
    $secretString = aws secretsmanager get-secret-value --secret-id $SecretName --region $Region --query SecretString --output text
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($secretString)) {
        throw "Unable to retrieve a database URL from Secrets Manager secret '$SecretName'."
    }

    $databaseUrl = Resolve-DatabaseUrl -SecretString $secretString
    if ([string]::IsNullOrWhiteSpace($databaseUrl)) {
        throw "The retrieved secret did not contain a usable DATABASE_URL value."
    }

    $env:DATABASE_URL = $databaseUrl

    $databaseEndpoint = Resolve-DatabaseEndpoint -DatabaseUrl $databaseUrl
    if ($null -ne $databaseEndpoint -and -not (Test-TcpConnectivity -EndpointHost $databaseEndpoint.Host -Port $databaseEndpoint.Port)) {
        throw "Aurora endpoint '$($databaseEndpoint.Host):$($databaseEndpoint.Port)' is not reachable from this machine. The cluster is private; run this script from a host with VPC access or use the Aurora Data API instead of a direct EF connection."
    }

    if ($ResetToZero) {
        Write-Host "Resetting Aurora schema to migration 0 via EF Core."
        Invoke-DotnetEfDatabaseUpdate -Migration "0"
    }

    Write-Host "Applying Aurora schema to migration '$TargetMigration'."
    Invoke-DotnetEfDatabaseUpdate -Migration $TargetMigration

    Write-Host "Aurora schema update completed."
    Write-Host "Next step: re-import the real QuickBooks files and validate /api/workspace/snapshot plus /api/workspace/knowledge."
}
finally {
    if ($null -eq $previousDatabaseUrl) {
        Remove-Item Env:DATABASE_URL -ErrorAction SilentlyContinue
    }
    else {
        $env:DATABASE_URL = $previousDatabaseUrl
    }

    Pop-Location
}