#!/usr/bin/env bash
# Thin wrapper for Amplify / shell workflows. Prefer the Node script directly on Windows.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
exec node "$ROOT/Scripts/generate-townhub-appsettings.cjs" "$@"
