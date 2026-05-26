# Launch Grok Build TUI for Cursor / VS Code integrated terminals on Windows.
#
# Plain `grok` often shows a blank screen here because:
#   1) grok.exe is not on PATH unless you add ~/.grok/bin yourself
#   2) the default alternate-screen fullscreen TUI does not render in embedded terminals
#
# Usage:
#   .\Scripts\start-grok-tui.ps1
#   .\Scripts\start-grok-tui.ps1 -Resume
#   .\Scripts\start-grok-tui.ps1 -SessionId left-nav-cloudwatch-v1
#   .\Scripts\start-grok-tui.ps1 -WindowsTerminal   # opens in Windows Terminal (best TUI)

param(
    [switch]$Resume,
    [string]$SessionId = "",
    [switch]$WindowsTerminal
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$GrokBinDir = Join-Path $env:USERPROFILE ".grok\bin"
$GrokExe = Join-Path $GrokBinDir "grok.exe"
$AuthFile = Join-Path $env:USERPROFILE ".grok\auth.json"

if (-not (Test-Path $GrokExe)) {
    Write-Host "Grok CLI not found at $GrokExe" -ForegroundColor Red
    Write-Host "Install with: .\Scripts\setup-grok.ps1 -Install" -ForegroundColor Yellow
    exit 1
}

if (-not $env:XAI_API_KEY -and -not (Test-Path $AuthFile)) {
    Write-Host "Grok is not authenticated." -ForegroundColor Red
    Write-Host "Run: .\Scripts\setup-grok.ps1 -Login" -ForegroundColor Yellow
    exit 1
}

# Ensure this session can find grok for any child shells.
$env:Path = "$GrokBinDir;$env:Path"

# Help Grok detect an embedded IDE terminal and render inline UI correctly.
if (-not $env:TERM -or $env:TERM -eq "dumb") {
    $env:TERM = "xterm-256color"
}
if (-not $env:COLORTERM) {
    $env:COLORTERM = "truecolor"
}
if (-not $env:TERM_PROGRAM) {
    $env:TERM_PROGRAM = "vscode"
}

$grokArgs = @(
    "--cwd", $RepoRoot,
    "--no-alt-screen"
)

if ($Resume -and $SessionId) {
    $grokArgs += @("--resume", $SessionId)
}
elseif ($Resume) {
    $grokArgs += @("-c")
}

$hostWidth = $Host.UI.RawUI.WindowSize.Width
if ($hostWidth -lt 100) {
    Write-Host "Tip: widen this terminal to at least 100 columns (current: $hostWidth)." -ForegroundColor DarkYellow
}

Write-Host "Starting Grok TUI (inline mode for Cursor)..." -ForegroundColor Cyan
Write-Host "  $GrokExe $($grokArgs -join ' ')" -ForegroundColor DarkGray
Write-Host "  Quit with Ctrl+D (or Ctrl+Q). Resume later: .\Scripts\start-grok-tui.ps1 -Resume" -ForegroundColor DarkGray
Write-Host ""

if ($WindowsTerminal) {
    $wt = Get-Command wt.exe -ErrorAction SilentlyContinue
    if (-not $wt) {
        Write-Host "Windows Terminal (wt.exe) not found. Install from Microsoft Store or run without -WindowsTerminal." -ForegroundColor Red
        exit 1
    }

    $argLine = ($grokArgs | ForEach-Object {
        if ($_ -match '\s') { "'$($_.Replace("'", "''"))'" } else { $_ }
    }) -join ' '

    & wt.exe -d $RepoRoot pwsh -NoExit -Command "& '$GrokExe' $argLine"
    exit $LASTEXITCODE
}

Push-Location $RepoRoot
try {
    & $GrokExe @grokArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
