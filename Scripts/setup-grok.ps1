# Configure Grok Build CLI for this repo (e2e per https://docs.x.ai/build/overview).
#
# Usage:
#   .\Scripts\setup-grok.ps1              # verify install + auth + inspect
#   .\Scripts\setup-grok.ps1 -Install     # install/update Grok CLI first
#   .\Scripts\setup-grok.ps1 -Login       # open browser OAuth login

param(
    [switch]$Install,
    [switch]$Login,
    [switch]$InspectOnly
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$GrokBinDir = Join-Path $env:USERPROFILE ".grok\bin"
$GrokExe = Join-Path $GrokBinDir "grok.exe"
$UserConfig = Join-Path $env:USERPROFILE ".grok\config.toml"

function Ensure-GrokPath {
    if ($env:Path -notlike "*$GrokBinDir*") {
        $env:Path = "$GrokBinDir;$env:Path"
    }
}

function Install-GrokCli {
    Write-Host "Installing Grok Build CLI (PowerShell installer) ..."
    irm https://x.ai/cli/install.ps1 | iex
    Ensure-GrokPath
}

function Test-GrokAuth {
    if ($env:XAI_API_KEY) {
        return "XAI_API_KEY"
    }

    $authPath = Join-Path $env:USERPROFILE ".grok\auth.json"
    if (Test-Path $authPath) {
        return "oauth-cache"
    }

    return ""
}

function Ensure-UserConfig {
    $desired = @"
[cli]
auto_update = true

[models]
default = "grok-build"

[toolset.bash]
timeout_secs = 600.0
"@

    if (-not (Test-Path $UserConfig)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $UserConfig -Parent) | Out-Null
        Set-Content -Path $UserConfig -Value $desired -Encoding UTF8
        Write-Host "Created user config: $UserConfig"
        return
    }

    $existing = Get-Content $UserConfig -Raw
    if ($existing -notmatch '\[models\]' -or $existing -notmatch 'grok-build') {
        Write-Host 'User config exists - ensure [models] default = "grok-build" in' $UserConfig
    }
}

if ($Install -or -not (Test-Path $GrokExe)) {
    if (-not (Test-Path $GrokExe)) {
        Install-GrokCli
    }
    else {
        Write-Host "Updating Grok CLI ..."
        Ensure-GrokPath
        & $GrokExe update
    }
}

if (-not (Test-Path $GrokExe)) {
    throw "Grok CLI not found at $GrokExe. Run: .\Scripts\setup-grok.ps1 -Install"
}

Ensure-GrokPath

if ($Login) {
    Push-Location $RepoRoot
    try {
        & $GrokExe login
    }
    finally {
        Pop-Location
    }
}

$version = & $GrokExe --version 2>&1 | Out-String
Write-Host $version.Trim()

$auth = Test-GrokAuth
if (-not $auth) {
    Write-Host ""
    Write-Host "Authentication required (https://docs.x.ai/build/overview):"
    Write-Host "  .\Scripts\setup-grok.ps1 -Login"
    Write-Host "  or set XAI_API_KEY for headless/CI"
    exit 1
}

Write-Host "Auth: $auth"
Ensure-UserConfig

Push-Location $RepoRoot
try {
    Write-Host ""
    Write-Host "=== grok inspect ==="
    & $GrokExe inspect
}
finally {
    Pop-Location
}

if ($InspectOnly) {
    exit 0
}

Write-Host ""
Write-Host "=== Next: headless backend plan ==="
Write-Host "  .\Scripts\run-grok-backend-plan.ps1 -Remaining -Foreground"
Write-Host "  .\Scripts\run-grok-backend-plan.ps1 -Continue -Foreground"
