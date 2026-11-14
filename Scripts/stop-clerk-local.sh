#!/usr/bin/env bash
# Stop Mac clerk-local Widget API/client launchctl jobs.
# Leaves NAS Synology Postgres running (it is shared office infrastructure).
set -euo pipefail

STATE_DIR="${WILEY_CLERK_STATE_DIR:-/tmp/wiley-clerk-local}"
LAUNCH_API_LABEL="wiley-widget-api-5231"
LAUNCH_CLIENT_LABEL="wiley-widget-client-5230"

log() { printf '[stop-clerk-local] %s\n' "$*"; }

launchctl remove "$LAUNCH_API_LABEL" 2>/dev/null || true
launchctl remove "$LAUNCH_CLIENT_LABEL" 2>/dev/null || true

for port in 5231 5230; do
  pids="$(lsof -t -iTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
  if [[ -n "$pids" ]]; then
    log "Killing listeners on $port: $pids"
    # shellcheck disable=SC2086
    kill $pids 2>/dev/null || true
  fi
done

rm -f "$STATE_DIR/api.url" "$STATE_DIR/client.url" "$STATE_DIR/api.launchctl" "$STATE_DIR/client.launchctl" 2>/dev/null || true
log "Stopped clerk-local API/client (NAS Postgres left running)."
