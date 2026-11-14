#!/usr/bin/env bash
# Fail when a Syncfusion license key is tracked in git.
# The key belongs in Amplify / Secrets Manager / the environment, then RegisterLicense at startup.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

# Split so this script does not contain the key prefix it searches for.
prefix='Ngo9''Big'
if git grep -I -n -E "${prefix}[A-Za-z0-9+/=]{8,}" -- . >/dev/null; then
  echo "ERROR: A Syncfusion license key is committed. Remove it and load SYNCFUSION_LICENSE_KEY from configuration." >&2
  git grep -I -l -E "${prefix}[A-Za-z0-9+/=]{8,}" -- . >&2 || true
  exit 1
fi

echo "No committed Syncfusion license key."
