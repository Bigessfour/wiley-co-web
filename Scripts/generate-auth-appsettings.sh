#!/usr/bin/env bash
# Emit wwwroot/appsettings.Auth.local.json for staff Cognito (merged at WASM startup).
# Usage:
#   COGNITO_USER_POOL_ID=... COGNITO_APP_CLIENT_ID=... COGNITO_REGION=us-east-2 \
#     ./Scripts/generate-auth-appsettings.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
POOL="${COGNITO_USER_POOL_ID:-}"
CLIENT="${COGNITO_APP_CLIENT_ID:-}"
REGION="${COGNITO_REGION:-us-east-2}"

if [[ -z "$POOL" || -z "$CLIENT" ]]; then
  echo "ERROR: Set COGNITO_USER_POOL_ID and COGNITO_APP_CLIENT_ID (optional COGNITO_REGION)." >&2
  exit 1
fi

mkdir -p "$ROOT/wwwroot"
node -e '
  const fs = require("fs");
  const path = process.argv[1];
  const pool = process.env.COGNITO_USER_POOL_ID.trim();
  const client = process.env.COGNITO_APP_CLIENT_ID.trim();
  const region = (process.env.COGNITO_REGION || "us-east-2").trim();
  const payload = {
    Auth: {
      Enabled: true,
      Cognito: {
        Region: region,
        UserPoolId: pool,
        ClientId: client,
        Authority: `https://cognito-idp.${region}.amazonaws.com/${pool}`
      }
    }
  };
  fs.writeFileSync(path, JSON.stringify(payload, null, 2) + "\n");
' "$ROOT/wwwroot/appsettings.Auth.local.json"

echo "Wrote $ROOT/wwwroot/appsettings.Auth.local.json (Auth.Enabled=true)."
