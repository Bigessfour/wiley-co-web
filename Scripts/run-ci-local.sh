#!/usr/bin/env bash
# Mirror Wiley.co CI (.github/workflows/ci.yml) as far as macOS/local allows.
# Skips: TruffleHog (optional), Windows runners, Amplify deployment-guard (OIDC/main only).
#
# Usage:
#   ./Scripts/run-ci-local.sh              # pre-push gate (HighRisk + publish + Playwright CI specs)
#   ./Scripts/run-ci-local.sh --pre-push   # same as default
#   ./Scripts/run-ci-local.sh --full       # also run the full test matrix (slow)
#   ./Scripts/run-ci-local.sh --skip-playwright
#
# On success writes .local-ci-ok (gitignored) with the HEAD SHA so Cursor can
# block git push until this gate has been re-run after new commits.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

export DOTNET_NOLOGO=true
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
export DOTNET_CLI_TELEMETRY_OPTOUT=true
export PLAYWRIGHT_HTML_OPEN=never
export CI=true
export PLAYWRIGHT_CI_BROWSERS="${PLAYWRIGHT_CI_BROWSERS:-chromium}"
export FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true

# Prefer ~/.dotnet (or whatever `dotnet` resolves) over a login-shell /usr/local/share/dotnet that may lack net9.
if [[ -z "${DOTNET_ROOT:-}" ]]; then
  _dotnet_bin="$(command -v dotnet || true)"
  if [[ -n "$_dotnet_bin" ]]; then
    export DOTNET_ROOT="$(cd "$(dirname "$_dotnet_bin")" && pwd)"
  elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
    export DOTNET_ROOT="$HOME/.dotnet"
  fi
fi
if [[ -n "${DOTNET_ROOT:-}" ]]; then
  export PATH="$DOTNET_ROOT:$PATH"
fi

SKIP_PLAYWRIGHT=0
# Default is the pre-push gate (skip full matrix). Use --full for GH-parity length.
SKIP_FULL_TESTS=1
for arg in "$@"; do
  case "$arg" in
    --pre-push) SKIP_FULL_TESTS=1 ;;
    --full) SKIP_FULL_TESTS=0 ;;
    --skip-full-tests) SKIP_FULL_TESTS=1 ;; # back-compat
    --skip-playwright) SKIP_PLAYWRIGHT=1 ;;
    -h|--help)
      echo "Usage: $0 [--pre-push|--full] [--skip-playwright]"
      echo "  default/--pre-push  HighRisk + publish + Playwright CI smoke/highrisk (recommended before push)"
      echo "  --full              Also run the full test matrix"
      echo "  --skip-playwright   Skip Playwright (not recommended before push)"
      exit 0
      ;;
    *)
      echo "Unknown arg: $arg" >&2
      exit 2
      ;;
  esac
done

fail() { echo "FAIL: $*" >&2; exit 1; }
step() { echo; echo "=== $* ==="; }

TEST_PROJECTS=(
  "tests/WileyCoWeb.ComponentTests/WileyCoWeb.ComponentTests.csproj"
  "tests/WileyCoWeb.IntegrationTests/WileyCoWeb.IntegrationTests.csproj"
  "tests/WileyWidget.Tests/WileyWidget.Tests.csproj"
  "tests/WileyCoWeb.E2ETests/WileyCoWeb.E2ETests.csproj"
)

HIGHRISK_PROJECTS=(
  "tests/WileyCoWeb.ComponentTests/WileyCoWeb.ComponentTests.csproj"
  "tests/WileyCoWeb.IntegrationTests/WileyCoWeb.IntegrationTests.csproj"
  "tests/WileyWidget.Tests/WileyWidget.Tests.csproj"
)

STAMP_FILE="$ROOT/.local-ci-ok"

step "secrets-guard (optional TruffleHog)"
if command -v trufflehog >/dev/null 2>&1; then
  trufflehog filesystem "$ROOT" --only-verified || fail "TruffleHog found verified secrets"
else
  echo "trufflehog not installed — skipped (CI still runs it on GH)"
fi

step "highrisk-tests"
for project in "${HIGHRISK_PROJECTS[@]}"; do
  echo "-> $project"
  dotnet restore "$project" --locked-mode
  dotnet build "$project" --configuration Release --no-restore -p:ContinuousIntegrationBuild=true
  dotnet test "$project" --configuration Release --no-build --no-restore --filter "Category=HighRisk" --verbosity minimal
done

if [[ "$SKIP_FULL_TESTS" -eq 0 ]]; then
  step "test (full matrix)"
  for project in "${TEST_PROJECTS[@]}"; do
    name="$(basename "$(dirname "$project")")"
    echo "-> $name"
    mkdir -p "TestResults/$name"
    dotnet restore "$project" --locked-mode
    dotnet build "$project" --configuration Release --no-restore -p:ContinuousIntegrationBuild=true
    dotnet test "$project" --configuration Release --no-build --no-restore --verbosity minimal \
      --settings coverlet.runsettings \
      --collect:"XPlat Code Coverage" \
      --results-directory "TestResults/$name" \
      --blame-hang-timeout 5m
  done
else
  echo "Skipping full test matrix (pre-push gate). Use --full for GH-length matrix."
fi

step "build-and-publish"
dotnet restore WileyCoWeb.slnx --locked-mode
if ! dotnet workload list | grep -q wasm-tools; then
  echo "Installing wasm-tools workload..."
  dotnet workload install wasm-tools --skip-manifest-update
fi
dotnet build WileyCoWeb.slnx --configuration Release --no-restore -p:ContinuousIntegrationBuild=true
rm -rf publish_output api_output
dotnet publish WileyCoWeb.csproj -c Release -o publish_output --no-restore -p:ContinuousIntegrationBuild=true
dotnet publish WileyCoWeb.Api/WileyCoWeb.Api.csproj --configuration Release --no-build -o api_output -p:ContinuousIntegrationBuild=true
test -d publish_output/wwwroot || fail "publish_output/wwwroot missing"

if [[ "$SKIP_PLAYWRIGHT" -eq 0 ]]; then
  step "playwright-ui"
  npm ci
  npx playwright install chromium
  npm run playwright:test:ci:smoke
  npm run playwright:test:ci:highrisk
  node ./Scripts/assert-playwright-results.mjs
else
  echo "Skipping Playwright (--skip-playwright)"
fi

HEAD_SHA="$(git rev-parse HEAD 2>/dev/null || echo unknown)"
{
  echo "sha=$HEAD_SHA"
  echo "at=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "mode=$([ "$SKIP_FULL_TESTS" -eq 1 ] && echo pre-push || echo full)"
  echo "playwright=$([ "$SKIP_PLAYWRIGHT" -eq 0 ] && echo yes || echo no)"
} >"$STAMP_FILE"

step "done"
echo "Local CI mirror passed (deployment-guard skipped — main/OIDC only)."
echo "Wrote $STAMP_FILE for HEAD $HEAD_SHA — git push is unlocked for this commit."
