#!/usr/bin/env bash
# Start the Mac clerk path: NAS Synology Postgres (Tailscale) + Widget API (5231) + Blazor client (5230).
# Durable via launchctl so Cursor shell SIGTERM cannot wipe the API.
# Postgres is native Synology Community PostgreSQL 17 on mr-storage:5433 (not Docker).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
STATE_DIR="${WILEY_CLERK_STATE_DIR:-/tmp/wiley-clerk-local}"
API_DIR="$ROOT/WileyCoWeb.Api"
CLIENT_DIR="$ROOT"
API_URL="${WILEY_WORKSPACE_API_BASE_ADDRESS:-http://127.0.0.1:5231}"
CLIENT_URL="http://127.0.0.1:5230"
DOTNET_BIN="${DOTNET_BIN:-$(command -v dotnet)}"
LAUNCH_API_LABEL="wiley-widget-api-5231"
LAUNCH_CLIENT_LABEL="wiley-widget-client-5230"
NAS_PG_HOST="${WILEYCO_PGHOST:-mr-storage}"
NAS_PG_PORT="${WILEYCO_PGPORT:-5433}"

mkdir -p "$STATE_DIR"
API_LOG="$STATE_DIR/api.log"
CLIENT_LOG="$STATE_DIR/client.log"

log() { printf '[start-clerk-local] %s\n' "$*"; }
die() { printf '[start-clerk-local] ERROR: %s\n' "$*" >&2; exit 1; }

[[ -x "$DOTNET_BIN" || -n "$(command -v "$DOTNET_BIN")" ]] || die "dotnet not found"
[[ -f "$API_DIR/appsettings.Development.local.json" ]] || die "missing Development.local.json — restore ConnectionStrings (Host=$NAS_PG_HOST;Port=$NAS_PG_PORT;Database=wileyco_dev;Username=wiley_app) before starting"

# Reject empty {} local settings; require NAS host/user/port (fatal)
python3 - "$API_DIR/appsettings.Development.local.json" "$NAS_PG_HOST" "$NAS_PG_PORT" <<'PY' || die "Development.local.json is empty, invalid, or not pointed at NAS Postgres"
import json, sys
path, host, port = sys.argv[1], sys.argv[2], sys.argv[3]
with open(path) as f:
    data = json.load(f)
cs = (data.get("ConnectionStrings") or {}).get("DefaultConnection") or ""
if not str(cs).strip():
    raise SystemExit("DefaultConnection missing")
compact = cs.lower().replace(" ", "")
errors = []
if f"host={host.lower()}" not in compact:
    errors.append(f"Host must be {host}")
if f"port={port}" not in compact:
    errors.append(f"Port must be {port}")
if "username=wiley_app" not in compact:
    errors.append("Username must be wiley_app")
if "database=wileyco_dev" not in compact:
    errors.append("Database must be wileyco_dev")
if "use_development_local_or_user_secrets" in compact:
    errors.append("Password still looks like the Development.json sentinel")
allow = (data.get("Database") or {}).get("AllowDegradedStartup", None)
if allow is True:
    errors.append("AllowDegradedStartup must be false for clerk path")
if errors:
    print("; ".join(errors), file=sys.stderr)
    raise SystemExit(1)
PY

log "Checking NAS Postgres at ${NAS_PG_HOST}:${NAS_PG_PORT} (Synology Community PG, Tailscale)..."
if ! nc -z -G 3 "$NAS_PG_HOST" "$NAS_PG_PORT" 2>/dev/null; then
  die "cannot reach ${NAS_PG_HOST}:${NAS_PG_PORT} — join Tailscale and confirm Synology PostgreSQL is running"
fi
log "NAS Postgres port is open"

# Design-time factory reads this env first (avoids stale localhost design DB).
export ConnectionStrings__DefaultConnection
ConnectionStrings__DefaultConnection="$(
  python3 - "$API_DIR/appsettings.Development.local.json" <<'PY'
import json, sys
print(json.load(open(sys.argv[1]))["ConnectionStrings"]["DefaultConnection"])
PY
)"
[[ -n "${ConnectionStrings__DefaultConnection}" ]] || die "could not read DefaultConnection from Development.local.json"

log "Applying EF migrations against NAS wileyco_dev..."
(
  cd "$ROOT"
  "$DOTNET_BIN" ef database update \
    --project "$ROOT/src/WileyWidget.Data/WileyWidget.Data.csproj" \
    --startup-project "$API_DIR/WileyCoWeb.Api.csproj" \
    --context AppDbContext
) || die "EF migrate failed — refusing to start degraded"

# Workspace client local settings
printf '%s\n' "{\"WorkspaceApiBaseAddress\":\"${API_URL}\"}" > "$ROOT/wwwroot/appsettings.Workspace.local.json"
printf '%s\n' "{\"WorkspaceApiBaseAddress\":\"${API_URL}\"}" > "$ROOT/appsettings.Workspace.local.json"
log "Wrote Workspace.local.json -> $API_URL"

# Stop prior launchctl jobs if present
launchctl remove "$LAUNCH_API_LABEL" 2>/dev/null || true
launchctl remove "$LAUNCH_CLIENT_LABEL" 2>/dev/null || true
# Free ports if something else is listening
for port in 5231 5230; do
  pids="$(lsof -t -iTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
  if [[ -n "$pids" ]]; then
    log "Killing listeners on $port: $pids"
    # shellcheck disable=SC2086
    kill $pids 2>/dev/null || true
    sleep 1
  fi
done

: >"$API_LOG"
: >"$CLIENT_LOG"

log "Starting API on $API_URL (launchctl $LAUNCH_API_LABEL)..."
launchctl submit -l "$LAUNCH_API_LABEL" -- \
  /bin/bash -lc "export PATH=\"$PATH\"; export DOTNET_ROOT=\"${DOTNET_ROOT:-}\"; cd \"$API_DIR\" && exec env ASPNETCORE_ENVIRONMENT=Development \"$DOTNET_BIN\" run --project \"$API_DIR/WileyCoWeb.Api.csproj\" --no-launch-profile --urls \"$API_URL\" >>\"$API_LOG\" 2>&1"

log "Waiting for API /health..."
for i in $(seq 1 90); do
  code="$(curl -s -o /dev/null -w '%{http_code}' "$API_URL/health" || true)"
  if [[ "$code" == "200" ]]; then
    log "API healthy"
    break
  fi
  if [[ "$i" -eq 90 ]]; then
    tail -40 "$API_LOG" >&2 || true
    die "API did not become healthy (last HTTP $code). See $API_LOG"
  fi
  sleep 1
done

# Fail if degraded / in-memory fallback activated
if rg -qi 'DegradedModeActive=True|Activating in-memory degraded mode|running in degraded mode' "$API_LOG" 2>/dev/null; then
  die "API log reports degraded mode — clerk path requires real Postgres"
fi
if ! rg -q 'DatabaseConfigured=True' "$API_LOG" 2>/dev/null; then
  die "API log missing DatabaseConfigured=True — refusing clerk start"
fi
if ! rg -q 'DegradedModeActive=False' "$API_LOG" 2>/dev/null; then
  die "API log missing DegradedModeActive=False — refusing clerk start"
fi

log "Starting Blazor client on $CLIENT_URL..."
launchctl submit -l "$LAUNCH_CLIENT_LABEL" -- \
  /bin/bash -lc "export PATH=\"$PATH\"; export DOTNET_ROOT=\"${DOTNET_ROOT:-}\"; cd \"$CLIENT_DIR\" && exec env ASPNETCORE_ENVIRONMENT=Development WILEY_WORKSPACE_API_BASE_ADDRESS=\"$API_URL\" \"$DOTNET_BIN\" run --project \"$CLIENT_DIR/WileyCoWeb.csproj\" --no-launch-profile --urls \"$CLIENT_URL\" >>\"$CLIENT_LOG\" 2>&1"

log "Waiting for client..."
for i in $(seq 1 120); do
  code="$(curl -s -o /dev/null -w '%{http_code}' "$CLIENT_URL/" || true)"
  if [[ "$code" == "200" ]]; then
    log "Client ready"
    break
  fi
  if [[ "$i" -eq 120 ]]; then
    tail -40 "$CLIENT_LOG" >&2 || true
    die "Client did not become ready (last HTTP $code). See $CLIENT_LOG"
  fi
  sleep 1
done

# Snapshot smoke
snap_code="$(curl -s -o /dev/null -w '%{http_code}' "$API_URL/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026" || true)"
[[ "$snap_code" == "200" ]] || die "snapshot probe failed HTTP $snap_code"

printf '%s\n' "$API_URL" >"$STATE_DIR/api.url"
printf '%s\n' "$CLIENT_URL" >"$STATE_DIR/client.url"
printf '%s\n' "$LAUNCH_API_LABEL" >"$STATE_DIR/api.launchctl"
printf '%s\n' "$LAUNCH_CLIENT_LABEL" >"$STATE_DIR/client.launchctl"
printf '%s\n' "${NAS_PG_HOST}:${NAS_PG_PORT}" >"$STATE_DIR/postgres.endpoint"

log "Clerk local stack is up."
log "  Client:   $CLIENT_URL"
log "  API:      $API_URL"
log "  Postgres: ${NAS_PG_HOST}:${NAS_PG_PORT} (NAS native, wileyco_dev / wiley_app)"
log "  Logs:     $STATE_DIR"
log "Next: ./Scripts/reload-clerk-imports.sh   (optional FY2026 ledger reload)"
log "Status: ./Scripts/status-clerk-local.sh"
