[CmdletBinding()]
param(
    [string]$Region = "us-east-2",
    [string]$ClusterIdentifier = "wiley-co-aurora-db",
    [string]$Project = "src/WileyWidget.Data/WileyWidget.Data.csproj",
    [string]$StartupProject = "WileyCoWeb.Api/WileyCoWeb.Api.csproj",
    [string]$Context = "AppDbContext",
    [string]$Migration = "InitialCreate",
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ClusterConfiguration {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Identifier,
        [Parameter(Mandatory = $true)]
        [string]$AwsRegion
    )

    $clusterJson = aws rds describe-db-clusters --db-cluster-identifier $Identifier --region $AwsRegion --query "DBClusters[0].{Arn:DBClusterArn,SecretArn:MasterUserSecret.SecretArn,DatabaseName:DatabaseName,HttpEndpointEnabled:HttpEndpointEnabled,Status:Status}" --output json
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($clusterJson)) {
        throw "Unable to describe Aurora cluster '$Identifier'."
    }

    $cluster = $clusterJson | ConvertFrom-Json -ErrorAction Stop
    if ($cluster.Status -ne "available") {
        throw "Aurora cluster '$Identifier' is not available. Current status: '$($cluster.Status)'."
    }

    if (-not $cluster.HttpEndpointEnabled) {
        throw "Aurora cluster '$Identifier' does not have the HTTP endpoint enabled. Data API apply is unavailable."
    }

    if ([string]::IsNullOrWhiteSpace($cluster.Arn) -or [string]::IsNullOrWhiteSpace($cluster.SecretArn) -or [string]::IsNullOrWhiteSpace($cluster.DatabaseName)) {
        throw "Aurora cluster '$Identifier' is missing the ARN, master secret ARN, or database name required for Data API access."
    }

    return $cluster
}

function Split-SqlStatements {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SqlText
    )

    $statements = [System.Collections.Generic.List[string]]::new()
    $builder = [System.Text.StringBuilder]::new()
    $insideSingleQuote = $false
    $insideDoubleQuote = $false

    for ($index = 0; $index -lt $SqlText.Length; $index++) {
        $character = $SqlText[$index]

        if ($character -eq "'" -and -not $insideDoubleQuote) {
            [void]$builder.Append($character)

            if ($insideSingleQuote -and $index + 1 -lt $SqlText.Length -and $SqlText[$index + 1] -eq "'") {
                $index++
                [void]$builder.Append("'")
                continue
            }

            $insideSingleQuote = -not $insideSingleQuote
            continue
        }

        if ($character -eq '"' -and -not $insideSingleQuote) {
            $insideDoubleQuote = -not $insideDoubleQuote
            [void]$builder.Append($character)
            continue
        }

        if ($character -eq ';' -and -not $insideSingleQuote -and -not $insideDoubleQuote) {
            $statement = $builder.ToString().Trim()
            if (-not [string]::IsNullOrWhiteSpace($statement)) {
                $statements.Add($statement)
            }

            $builder.Clear() | Out-Null
            continue
        }

        [void]$builder.Append($character)
    }

    $lastStatement = $builder.ToString().Trim()
    if (-not [string]::IsNullOrWhiteSpace($lastStatement)) {
        $statements.Add($lastStatement)
    }

    return $statements
}

function Invoke-DataApiStatement {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Cluster,
        [Parameter(Mandatory = $true)]
        [string]$AwsRegion,
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $output = & aws rds-data execute-statement --resource-arn $Cluster.Arn --secret-arn $Cluster.SecretArn --database $Cluster.DatabaseName --region $AwsRegion --sql $Sql --output json 2>&1
    if ($LASTEXITCODE -eq 0) {
        return "executed"
    }

    $message = ($output | Out-String).Trim()
    if ($message -match "already exists" -or $message -match "duplicate key value violates unique constraint") {
        return "ignored"
    }

    throw "Data API execution failed: $message"
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$cluster = Get-ClusterConfiguration -Identifier $ClusterIdentifier -AwsRegion $Region
$scriptPath = Join-Path $repoRoot ("obj\aurora-{0}.sql" -f $Migration)

Push-Location $repoRoot
try {
    $arguments = @(
        "ef",
        "migrations",
        "script",
        "0",
        $Migration,
        "--project", $Project,
        "--startup-project", $StartupProject,
        "--context", $Context
    )

    if ($NoBuild) {
        $arguments += "--no-build"
    }

    & dotnet @arguments | Set-Content -Path $scriptPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $scriptPath)) {
        throw "Unable to generate the EF migration SQL for '$Migration'."
    }

    $sqlText = Get-Content -Path $scriptPath -Raw
    $statements = Split-SqlStatements -SqlText $sqlText

    $executedCount = 0
    $ignoredCount = 0
    foreach ($statement in $statements) {
        if ($statement.Equals("START TRANSACTION", [System.StringComparison]::OrdinalIgnoreCase) -or
            $statement.Equals("COMMIT", [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $result = Invoke-DataApiStatement -Cluster $cluster -AwsRegion $Region -Sql $statement
        if ($result -eq "executed") {
            $executedCount++
        }
        else {
            $ignoredCount++
        }
    }

    Write-Host "Aurora Data API migration apply completed. Executed $executedCount statements; ignored $ignoredCount already-present statements."
}
finally {
    Pop-Location
}