[CmdletBinding()]
param(
    [string]$Region = "us-east-2",
    [string]$ClusterIdentifier = "wiley-co-aurora-db-encrypted",
    [string]$SecretArn = "arn:aws:secretsmanager:us-east-2:570912405222:secret:wiley-widget/temp/copilot-data-api-readonly-sbIHYu",
    [string]$Project = "src/WileyWidget.Data/WileyWidget.Data.csproj",
    [string]$StartupProject = "WileyCoWeb.Api/WileyCoWeb.Api.csproj",
    [string]$Context = "AppDbContext",
    [string]$FromMigration = "0",
    [string]$ToMigration = "SchemaAlignmentProductionReadiness",
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

    if ([string]::IsNullOrWhiteSpace($cluster.Arn) -or [string]::IsNullOrWhiteSpace($cluster.DatabaseName)) {
        throw "Aurora cluster '$Identifier' is missing the ARN or database name required for Data API access."
    }

    if ([string]::IsNullOrWhiteSpace($cluster.SecretArn)) {
        $cluster | Add-Member -NotePropertyName SecretArn -NotePropertyValue $SecretArn -Force
    }

    if ([string]::IsNullOrWhiteSpace($cluster.SecretArn)) {
        throw "Aurora cluster '$Identifier' has no master user secret; pass -SecretArn with a JSON RDS credential secret."
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

    $inputObject = [ordered]@{
        resourceArn = $Cluster.Arn
        secretArn   = $Cluster.SecretArn
        database    = $Cluster.DatabaseName
        sql         = $Sql
    }
    $inputPath = Join-Path $env:TEMP ("rds-data-{0}.json" -f [Guid]::NewGuid().ToString("N"))
    $inputObject | ConvertTo-Json -Compress | Set-Content -Path $inputPath -Encoding utf8
    $inputUri = "file://" + ($inputPath -replace '\\', '/')

    $output = & aws rds-data execute-statement --region $AwsRegion --cli-input-json $inputUri --output json 2>&1
    Remove-Item -Path $inputPath -Force -ErrorAction SilentlyContinue
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
$scriptPath = Join-Path $repoRoot ("obj\aurora-{0}-to-{1}.sql" -f $FromMigration, $ToMigration)

Push-Location $repoRoot
try {
    $arguments = @(
        "ef",
        "migrations",
        "script",
        $FromMigration,
        $ToMigration,
        "--project", $Project,
        "--startup-project", $StartupProject,
        "--context", $Context
    )

    if ($NoBuild) {
        $arguments += "--no-build"
    }

    & dotnet @arguments | Set-Content -Path $scriptPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $scriptPath)) {
        throw "Unable to generate the EF migration SQL from '$FromMigration' to '$ToMigration'."
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