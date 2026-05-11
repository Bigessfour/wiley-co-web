[CmdletBinding()]
param(
    [string]$ImageTag = "wiley-playwright-ci"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

docker build --progress=plain -f Dockerfile.playwright-ci -t $ImageTag .

docker run --rm `
    --init `
    --ipc=host `
    -e CI=true `
    -e DOTNET_NOLOGO=1 `
    -e PLAYWRIGHT_BROWSERS_PATH=/ms-playwright `
    -w /workspace `
    $ImageTag bash ./Scripts/run-playwright-docker-proof.sh