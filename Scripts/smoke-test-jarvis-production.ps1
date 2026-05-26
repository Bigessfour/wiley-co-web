#Requires -Version 7.0
<#
.SYNOPSIS
  Smoke-test Jarvis health and chat on production App Runner with Cognito Bearer auth.
#>
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "https://mr7zeizxxd.us-east-2.awsapprunner.com",
    [string]$Region = "us-east-2"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$token = & (Join-Path $scriptRoot "get-cognito-smoke-token.ps1") -Region $Region
$headers = @{ Authorization = "Bearer $token" }

Write-Host "GET $ApiBaseUrl/api/ai/health"
$health = Invoke-RestMethod -Uri "$ApiBaseUrl/api/ai/health" -Headers $headers -Method Get
$health | ConvertTo-Json -Depth 6

$chatBody = @{
    question = "Smoke test: confirm Jarvis is reachable in production."
    contextSummary = "Production smoke test from Scripts/smoke-test-jarvis-production.ps1"
    selectedEnterprise = "Water Utility"
    selectedFiscalYear = 2026
} | ConvertTo-Json -Depth 6

Write-Host "POST $ApiBaseUrl/api/ai/chat"
$chat = Invoke-RestMethod `
    -Uri "$ApiBaseUrl/api/ai/chat" `
    -Headers $headers `
    -Method Post `
    -ContentType "application/json" `
    -Body $chatBody `
    -TimeoutSec 240

$chat | ConvertTo-Json -Depth 6
