#!/usr/bin/env bash
# Probe Mac clerk-local stack health, NAS Postgres, and Town Hub sibling URLs.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
API_URL="${WILEY_WORKSPACE_API_BASE_ADDRESS:-http://127.0.0.1:5231}"
CLIENT_URL="http://127.0.0.1:5230"
STATE_DIR="${WILEY_CLERK_STATE_DIR:-/tmp/wiley-clerk-local}"
NAS_PG_HOST="${WILEYCO_PGHOST:-mr-storage}"
NAS_PG_PORT="${WILEYCO_PGPORT:-5433}"

probe() {
  local name="$1" url="$2"
  local code
  code="$(curl -s -o /dev/null -w '%{http_code}' --connect-timeout 3 "$url" || echo 000)"
  printf '%-28s %s  %s\n' "$name" "$code" "$url"
}

echo "=== ports ==="
if nc -z -G 2 "$NAS_PG_HOST" "$NAS_PG_PORT" 2>/dev/null; then
  echo "${NAS_PG_HOST}:${NAS_PG_PORT}: open (NAS Synology PostgreSQL)"
else
  echo "${NAS_PG_HOST}:${NAS_PG_PORT}: closed"
fi
lsof -iTCP:5231 -sTCP:LISTEN 2>/dev/null | head -2 || echo "5231: none"
lsof -iTCP:5230 -sTCP:LISTEN 2>/dev/null | head -2 || echo "5230: none"

echo "=== health ==="
probe "api /health" "$API_URL/health"
probe "client /" "$CLIENT_URL/"
probe "snapshot Water Utility" "$API_URL/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026"

echo "=== imports / customers ==="
if curl -sf "$API_URL/api/imports/quickbooks/history" >/tmp/wiley-qb-history.json 2>/dev/null; then
  python3 - <<'PY'
import json
d=json.load(open("/tmp/wiley-qb-history.json"))
items=d.get("items") or []
rows=sum(i.get("rowCount") or 0 for i in items)
print(f"QB history: {len(items)} batches, {rows} rows — {d.get('statusMessage','')}")
PY
else
  echo "QB history: unreachable"
fi
if curl -sf "$API_URL/api/utility-customers" >/tmp/wiley-customers.json 2>/dev/null; then
  python3 - <<'PY'
import json
print(f"customers: {len(json.load(open('/tmp/wiley-customers.json')))}")
PY
else
  echo "customers: unreachable"
fi

echo "=== Town Hub sibling probes (Tailscale / Amplify) ==="
probe "Apartments NAS" "http://mr-storage:8082/"
probe "TIKR NAS" "http://mr-storage:8080/"
probe "Trash Amplify" "https://main.d2dke4s7zyafra.amplifyapp.com/"

echo "=== config flags ==="
python3 - <<PY
import json, re
from pathlib import Path
dev=json.load(open("$ROOT/WileyCoWeb.Api/appsettings.Development.json"))
local_path=Path("$ROOT/WileyCoWeb.Api/appsettings.Development.local.json")
prod=json.load(open("$ROOT/wwwroot/appsettings.Production.json"))
print("Development AllowDegradedStartup=", (dev.get("Database") or {}).get("AllowDegradedStartup"))
print("Development ApplyMigrations=", (dev.get("Database") or {}).get("ApplyMigrations"))
if local_path.exists():
  local=json.load(open(local_path))
  cs=(local.get("ConnectionStrings") or {}).get("DefaultConnection") or ""
  masked=re.sub(r'(?i)(Password|Pwd)=[^;]+', r'\1=***', cs)
  print("local ConnectionString=", masked or "(empty)")
  print("local AllowDegradedStartup=", (local.get("Database") or {}).get("AllowDegradedStartup"))
  print("local has XAI key=", bool((local.get("XAI") or {}).get("ApiKey")))
else:
  print("local settings: MISSING")
print("Production Auth.Enabled=", (prod.get("Auth") or {}).get("Enabled"))
ws=Path("$ROOT/wwwroot/appsettings.Workspace.local.json")
print("Workspace.local=", ws.read_text().strip() if ws.exists() else "MISSING")
PY

echo "=== state dir ==="
ls -la "$STATE_DIR" 2>/dev/null || echo "(none)"
