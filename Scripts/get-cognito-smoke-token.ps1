#Requires -Version 7.0
<#
.SYNOPSIS
  Obtain a Cognito access token for Wiley Widget production API smoke tests.

.DESCRIPTION
  Uses IAM credentials (copilot user or any principal with CognitoSmokeTest policy)
  to call AdminInitiateAuth against the Town of Wiley user pool. Credentials are read
  from Secrets Manager secret wiley-widget/temp/copilot-cognito-smoke unless overridden
  by -Username / -Password parameters.

  Example:
    $token = & .\Scripts\get-cognito-smoke-token.ps1
    Invoke-RestMethod -Uri "https://mr7zeizxxd.us-east-2.awsapprunner.com/api/ai/health" `
      -Headers @{ Authorization = "Bearer $token" }
#>
[CmdletBinding()]
param(
    [string]$Region = "us-east-2",
    [string]$SecretId = "wiley-widget/temp/copilot-cognito-smoke",
    [string]$UserPoolId,
    [string]$ClientId,
    [string]$Username,
    [string]$Password,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-SecretObject {
    param([string]$Id)
    $raw = aws secretsmanager get-secret-value `
        --secret-id $Id `
        --region $Region `
        --query SecretString `
        --output text
    if (-not $raw) {
        throw "Secret '$Id' returned an empty SecretString."
    }
    return ($raw | ConvertFrom-Json)
}

$config = $null
if (-not $UserPoolId -or -not $ClientId -or (-not $Username -and -not $Password)) {
    $config = Get-SecretObject -Id $SecretId
}

$UserPoolId = if ($UserPoolId) { $UserPoolId } else { $config.UserPoolId }
$ClientId = if ($ClientId) { $ClientId } else { $config.ClientId }
$Username = if ($Username) { $Username } else { $config.Username }
$Password = if ($Password) { $Password } else { $config.Password }

foreach ($name in @("UserPoolId", "ClientId", "Username", "Password")) {
    if (-not (Get-Variable -Name $name -ValueOnly)) {
        throw "Missing required value '$name'. Pass it explicitly or populate secret '$SecretId'."
    }
}

$authJson = aws cognito-idp admin-initiate-auth `
    --user-pool-id $UserPoolId `
    --client-id $ClientId `
    --auth-flow ADMIN_NO_SRP_AUTH `
    --auth-parameters "USERNAME=$Username,PASSWORD=$Password" `
    --region $Region `
    --output json

$auth = $authJson | ConvertFrom-Json
$challengeName = $auth.PSObject.Properties['ChallengeName']?.Value
if ($challengeName) {
    throw "Auth challenge '$challengeName' requires manual response. Reset the smoke-test password with admin-set-user-password."
}

$result = $auth.AuthenticationResult
if (-not $result) {
    throw "AdminInitiateAuth returned no AuthenticationResult. Response: $authJson"
}

$token = $result.AccessToken
if (-not $token) {
    throw "AdminInitiateAuth did not return an access token."
}

if ($Json) {
    [pscustomobject]@{
        AccessToken = $token
        ExpiresIn = $result.ExpiresIn
        TokenType = $result.TokenType
        UserPoolId = $UserPoolId
        ClientId = $ClientId
        Username = $Username
    } | ConvertTo-Json -Compress
}
else {
    $token
}
