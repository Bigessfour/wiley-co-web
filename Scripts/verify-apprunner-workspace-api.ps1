#Requires -Version 7.0
<#
.SYNOPSIS
  Smoke-check the Wiley Widget App Runner API after a deploy (health, snapshot, capital gap, debt coverage).

.EXAMPLE
  ./Scripts/verify-apprunner-workspace-api.ps1
  ./Scripts/verify-apprunner-workspace-api.ps1 -BaseUrl "https://mr7zeizxxd.us-east-2.awsapprunner.com"
#>
param(
    [string] $BaseUrl = "https://mr7zeizxxd.us-east-2.awsapprunner.com"
)

$ErrorActionPreference = "Stop"
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get
Write-Host "GET /health -> $health"

$snapshot = Invoke-RestMethod -Uri "$BaseUrl/api/workspace/snapshot" -Method Get
Write-Host "GET /api/workspace/snapshot -> enterprise=$($snapshot.selectedEnterprise) fy=$($snapshot.selectedFiscalYear)"

$capBody = '{"SelectedEnterprise":"Water Utility","SelectedFiscalYear":2037}'
$cap = Invoke-RestMethod -Uri "$BaseUrl/api/workspace/capital-gap" -Method Post -Body $capBody -ContentType "application/json"
Write-Host "POST /api/workspace/capital-gap -> items=$($cap.capitalItemCount) summary=$($cap.executiveSummary.Substring(0, [Math]::Min(80, $cap.executiveSummary.Length)))..."

$debtBody = '{"SelectedEnterprise":"Water Utility","SelectedFiscalYear":2037}'
$debt = Invoke-RestMethod -Uri "$BaseUrl/api/workspace/debt-coverage" -Method Post -Body $debtBody -ContentType "application/json"
Write-Host "POST /api/workspace/debt-coverage -> dscr=$($debt.debtServiceCoverageRatio) summary=$($debt.executiveSummary.Substring(0, [Math]::Min(80, $debt.executiveSummary.Length)))..."

Write-Host "All checks completed successfully."
