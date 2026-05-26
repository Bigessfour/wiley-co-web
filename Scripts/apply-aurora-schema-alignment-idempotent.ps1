[CmdletBinding()]
param(
    [string]$Region = "us-east-2",
    [string]$ClusterIdentifier = "wiley-co-aurora-db-encrypted",
    [string]$SecretArn = "arn:aws:secretsmanager:us-east-2:570912405222:secret:wiley-widget/temp/copilot-data-api-readonly-sbIHYu"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ClusterConfiguration {
    param(
        [Parameter(Mandatory = $true)][string]$Identifier,
        [Parameter(Mandatory = $true)][string]$AwsRegion,
        [Parameter(Mandatory = $true)][string]$CredentialSecretArn
    )

    $clusterJson = aws rds describe-db-clusters --db-cluster-identifier $Identifier --region $AwsRegion --query "DBClusters[0].{Arn:DBClusterArn,SecretArn:MasterUserSecret.SecretArn,DatabaseName:DatabaseName,HttpEndpointEnabled:HttpEndpointEnabled,Status:Status}" --output json
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($clusterJson)) {
        throw "Unable to describe Aurora cluster '$Identifier'."
    }

    $cluster = $clusterJson | ConvertFrom-Json -ErrorAction Stop
    if ($cluster.Status -ne "available") {
        throw "Aurora cluster '$Identifier' is not available. Status: '$($cluster.Status)'."
    }
    if (-not $cluster.HttpEndpointEnabled) {
        throw "Aurora cluster '$Identifier' does not have the HTTP endpoint enabled."
    }
    if ([string]::IsNullOrWhiteSpace($cluster.SecretArn)) {
        $cluster | Add-Member -NotePropertyName SecretArn -NotePropertyValue $CredentialSecretArn -Force
    }

    return $cluster
}

function Invoke-DataApiStatement {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Cluster,
        [Parameter(Mandatory = $true)][string]$AwsRegion,
        [Parameter(Mandatory = $true)][string]$Sql
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

    throw "Data API execution failed: $message`nSQL: $Sql"
}

$statements = @(
@'
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM "Charges"
        WHERE COALESCE("UtilityBillId", 0) = 0 AND COALESCE("BillId", 0) = 0
    ) THEN
        RAISE EXCEPTION 'Charges row(s) missing BillId/UtilityBillId';
    END IF;
END $$;
'@,
'UPDATE "Charges" SET "UtilityBillId" = "BillId" WHERE ("UtilityBillId" IS NULL OR "UtilityBillId" = 0) AND "BillId" IS NOT NULL;',
'ALTER TABLE "Charges" DROP CONSTRAINT IF EXISTS "FK_Charges_UtilityBills_BillId";',
'DROP INDEX IF EXISTS "IX_Charges_BillId";',
'ALTER TABLE "Charges" DROP COLUMN IF EXISTS "BillId";',
'ALTER TABLE "Charges" ALTER COLUMN "UtilityBillId" SET NOT NULL;',
'ALTER TABLE "BudgetInteraction" DROP CONSTRAINT IF EXISTS "FK_BudgetInteraction_Enterprises_EnterpriseId";',
'ALTER TABLE "BudgetInteraction" DROP COLUMN IF EXISTS "EnterpriseId";',
'ALTER TABLE "UtilityCustomers" ADD COLUMN IF NOT EXISTS "EnterpriseId" integer NULL;',
'CREATE INDEX IF NOT EXISTS "IX_UtilityCustomers_EnterpriseId" ON "UtilityCustomers" ("EnterpriseId");',
'ALTER TABLE ledger_entries ADD COLUMN IF NOT EXISTS applied_allocation_profile_id bigint NULL;',
'ALTER TABLE ledger_entries ADD COLUMN IF NOT EXISTS applied_allocation_profile_name character varying(200) NULL;',
'ALTER TABLE ledger_entries ADD COLUMN IF NOT EXISTS applied_routing_rule_id bigint NULL;',
'ALTER TABLE ledger_entries ADD COLUMN IF NOT EXISTS applied_routing_rule_name character varying(200) NULL;',
'ALTER TABLE ledger_entries ADD COLUMN IF NOT EXISTS original_entry_scope character varying(100) NULL;',
'ALTER TABLE ledger_entries ADD COLUMN IF NOT EXISTS routing_allocation_percent numeric(18,2) NULL;',
'ALTER TABLE ledger_entries ADD COLUMN IF NOT EXISTS routing_reason character varying(500) NULL;',
'ALTER TABLE ledger_entries ADD COLUMN IF NOT EXISTS source_amount numeric(18,2) NULL;',
'CREATE INDEX IF NOT EXISTS "IX_ledger_entries_entry_scope" ON ledger_entries (entry_scope);',
'CREATE INDEX IF NOT EXISTS "IX_BudgetEntries_SourceFilePath" ON "BudgetEntries" ("SourceFilePath");',
'CREATE INDEX IF NOT EXISTS "IX_AuditEntries_Timestamp" ON "AuditEntries" ("Timestamp");',
'CREATE INDEX IF NOT EXISTS "IX_AuditEntries_EntityType_EntityId_Timestamp" ON "AuditEntries" ("EntityType", "EntityId", "Timestamp");',
@'
CREATE TABLE IF NOT EXISTS "ApartmentUnitTypes" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "EnterpriseId" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "BedroomCount" integer NOT NULL,
    "UnitCount" integer NOT NULL,
    "MonthlyRent" numeric(18,2) NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "ModifiedDate" timestamp with time zone NULL,
    "CreatedBy" text NULL,
    "ModifiedBy" text NULL,
    "IsDeleted" boolean NOT NULL,
    "DeletedDate" timestamp with time zone NULL,
    "DeletedBy" text NULL,
    CONSTRAINT "PK_ApartmentUnitTypes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ApartmentUnitTypes_Enterprises_EnterpriseId" FOREIGN KEY ("EnterpriseId") REFERENCES "Enterprises" ("Id") ON DELETE CASCADE
);
'@,
'CREATE UNIQUE INDEX IF NOT EXISTS "IX_ApartmentUnitTypes_EnterpriseId_Name" ON "ApartmentUnitTypes" ("EnterpriseId", "Name");',
@'
CREATE TABLE IF NOT EXISTS quickbooks_allocation_profiles (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    name character varying(200) NOT NULL,
    description character varying(500) NULL,
    is_active boolean NOT NULL,
    CONSTRAINT "PK_quickbooks_allocation_profiles" PRIMARY KEY (id)
);
'@,
'CREATE INDEX IF NOT EXISTS "IX_quickbooks_allocation_profiles_is_active" ON quickbooks_allocation_profiles (is_active);',
@'
CREATE TABLE IF NOT EXISTS quickbooks_allocation_targets (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    allocation_profile_id bigint NOT NULL,
    enterprise_name character varying(100) NOT NULL,
    allocation_percent numeric(18,2) NOT NULL,
    CONSTRAINT "PK_quickbooks_allocation_targets" PRIMARY KEY (id),
    CONSTRAINT "FK_quickbooks_allocation_targets_quickbooks_allocation_profiles_allocation_profile_id" FOREIGN KEY (allocation_profile_id) REFERENCES quickbooks_allocation_profiles (id) ON DELETE CASCADE
);
'@,
'CREATE INDEX IF NOT EXISTS "IX_quickbooks_allocation_targets_allocation_profile_id" ON quickbooks_allocation_targets (allocation_profile_id);',
@'
CREATE TABLE IF NOT EXISTS quickbooks_routing_rules (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    name character varying(200) NOT NULL,
    description character varying(500) NULL,
    priority integer NOT NULL,
    is_active boolean NOT NULL,
    source_file_pattern character varying(200) NULL,
    default_enterprise_pattern character varying(200) NULL,
    account_pattern character varying(200) NULL,
    memo_pattern character varying(200) NULL,
    name_pattern character varying(200) NULL,
    split_account_pattern character varying(200) NULL,
    target_enterprise character varying(100) NULL,
    allocation_profile_id bigint NULL,
    CONSTRAINT "PK_quickbooks_routing_rules" PRIMARY KEY (id),
    CONSTRAINT "FK_quickbooks_routing_rules_quickbooks_allocation_profiles_allocation_profile_id" FOREIGN KEY (allocation_profile_id) REFERENCES quickbooks_allocation_profiles (id) ON DELETE SET NULL
);
'@,
'CREATE INDEX IF NOT EXISTS "IX_quickbooks_routing_rules_allocation_profile_id" ON quickbooks_routing_rules (allocation_profile_id);',
'CREATE INDEX IF NOT EXISTS "IX_quickbooks_routing_rules_is_active" ON quickbooks_routing_rules (is_active);',
'CREATE INDEX IF NOT EXISTS "IX_quickbooks_routing_rules_priority" ON quickbooks_routing_rules (priority);',
@'
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_UtilityCustomers_Enterprises_EnterpriseId'
    ) THEN
        ALTER TABLE "UtilityCustomers"
            ADD CONSTRAINT "FK_UtilityCustomers_Enterprises_EnterpriseId"
            FOREIGN KEY ("EnterpriseId") REFERENCES "Enterprises" ("Id") ON DELETE SET NULL;
    END IF;
END $$;
'@,
@'
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260525204607_SchemaAlignmentProductionReadiness', '9.0.0'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260525204607_SchemaAlignmentProductionReadiness'
);
'@
)

$cluster = Get-ClusterConfiguration -Identifier $ClusterIdentifier -AwsRegion $Region -CredentialSecretArn $SecretArn
$executedCount = 0
$ignoredCount = 0
$index = 0
foreach ($statement in $statements) {
    $index++
    Write-Host "Applying statement $index/$($statements.Count)..."
    $result = Invoke-DataApiStatement -Cluster $cluster -AwsRegion $Region -Sql $statement
    if ($result -eq "executed") { $executedCount++ } else { $ignoredCount++ }
}

Write-Host "Aurora idempotent schema alignment completed. Executed $executedCount statements; ignored $ignoredCount already-present statements."
