# Run Wiley Widget backend production readiness plan via Grok Build CLI (headless).
# Docs: https://docs.x.ai/build/overview · https://docs.x.ai/build/cli/headless-scripting
#
# Usage:
#   .\Scripts\run-grok-backend-plan.ps1                    # full plan (new session)
#   .\Scripts\run-grok-backend-plan.ps1 -Remaining         # remaining todos only (recommended)
#   .\Scripts\run-grok-backend-plan.ps1 -Continue          # resume named headless session (-s)
#   .\Scripts\run-grok-backend-plan.ps1 -Continue -Foreground
#   .\Scripts\setup-grok.ps1                               # install + auth + inspect first

param(
    [switch]$Continue,
    [switch]$Foreground,
    [switch]$Remaining,
    [switch]$Setup,
    [string]$SessionId = "backend-prod-readiness-v4",
    [string]$ResumeSessionId = "",
    [string]$Model = "grok-build",
    [int]$MaxTurns = 400
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$GrokExe = Join-Path $env:USERPROFILE ".grok\bin\grok.exe"
$SetupScript = Join-Path $PSScriptRoot "setup-grok.ps1"

if ($Continue -and -not $Remaining) {
    $Remaining = $true
}

$PromptFile = if ($Remaining) {
    Join-Path $RepoRoot ".grok\prompts\backend-production-readiness-remaining.md"
} else {
    Join-Path $RepoRoot ".grok\prompts\backend-production-readiness.md"
}
$LogDir = Join-Path $RepoRoot ".grok\logs"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$StdoutLog = Join-Path $LogDir "backend-production-readiness-$Timestamp.log"
$StderrLog = Join-Path $LogDir "backend-production-readiness-$Timestamp.err.log"
$RulesText = "Implement the attached prompt checklist completely. Use Grok CLI tools only. Do not ask the user for confirmation between todos."
$ContinuePrompt = "Continue the backend production readiness work from the prior session. Verify what is already done in the working tree before re-implementing. Complete all remaining todos and run the verification gate."

function Quote-Arg([string]$Value) {
    if ($Value -match '[\s"]') {
        return '"' + ($Value -replace '"', '\"') + '"'
    }
    return $Value
}

function Test-GrokAuth {
    if ($env:XAI_API_KEY) { return $true }
    return Test-Path (Join-Path $env:USERPROFILE ".grok\auth.json")
}

if ($Setup) {
    & $SetupScript -InspectOnly
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not (Test-Path $GrokExe)) {
    throw "Grok CLI not found. Run: .\Scripts\setup-grok.ps1 -Install"
}

if (-not (Test-GrokAuth)) {
    throw "Grok not authenticated. Run: .\Scripts\setup-grok.ps1 -Login  (or set XAI_API_KEY)"
}

if (-not $Continue -and -not (Test-Path $PromptFile)) {
    throw "Prompt file missing: $PromptFile"
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$env:Path = "$(Split-Path $GrokExe -Parent);$env:Path"

# Headless per https://docs.x.ai/build/cli/headless-scripting
$grokArgs = @(
    "--cwd", $RepoRoot,
    "-m", $Model,
    "--always-approve",
    "--max-turns", "$MaxTurns",
    "--check",
    "--output-format", "plain",
    "--no-alt-screen",
    "--rules", $RulesText
)

if ($Continue) {
    if ($ResumeSessionId) {
        $grokArgs = @(
            "-p", $ContinuePrompt,
            "--resume", $ResumeSessionId
        ) + $grokArgs
        Write-Host "Resuming Grok session UUID '$ResumeSessionId' in $RepoRoot ..."
    }
    else {
        $grokArgs = @(
            "-p", $ContinuePrompt
        ) + $grokArgs + @("-s", $SessionId)
        Write-Host "Resuming Grok named session '$SessionId' in $RepoRoot ..."
    }
}
else {
    $grokArgs = @(
        "--prompt-file", $PromptFile
    ) + $grokArgs + @("-s", $SessionId)
    Write-Host "Starting Grok session '$SessionId' in $RepoRoot ..."
    Write-Host "Prompt: $PromptFile"
}

Write-Host "Model: $Model"
Write-Host "Stdout log: $StdoutLog"
Write-Host "Stderr log: $StderrLog"

Push-Location $RepoRoot
try {
    if ($Foreground) {
        & $GrokExe @grokArgs 2>&1 | Tee-Object -FilePath $StdoutLog
        exit $LASTEXITCODE
    }

    $argLine = ($grokArgs | ForEach-Object { Quote-Arg $_ }) -join ' '
    $proc = Start-Process `
        -FilePath $GrokExe `
        -ArgumentList $argLine `
        -WorkingDirectory $RepoRoot `
        -RedirectStandardOutput $StdoutLog `
        -RedirectStandardError $StderrLog `
        -PassThru `
        -NoNewWindow

    Write-Host "Grok PID: $($proc.Id) - monitor logs above. Resume with: .\Scripts\run-grok-backend-plan.ps1 -Continue"
}
finally {
    Pop-Location
}
