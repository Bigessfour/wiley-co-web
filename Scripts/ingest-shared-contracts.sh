#!/usr/bin/env bash
# Prompt 4 — file → HTTPS ingest for shared-contracts examples.
# Posts to Wiley Widget /api/reference-data/* only (not QuickBooks import).
#
# Usage:
#   ./Scripts/ingest-shared-contracts.sh
#   WILEY_WORKSPACE_API_BASE_ADDRESS=https://api.example ./Scripts/ingest-shared-contracts.sh
#   ./Scripts/ingest-shared-contracts.sh --validate   # optional ajv if installed
#   ./Scripts/ingest-shared-contracts.sh --dir /path/to/examples
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
API_BASE="${WILEY_WORKSPACE_API_BASE_ADDRESS:-http://127.0.0.1:5231}"
EXAMPLES_DIR="$ROOT/shared-contracts/examples"
SCHEMAS_DIR="$ROOT/shared-contracts/schemas/v1"
VALIDATE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --validate) VALIDATE=1; shift ;;
    --dir) EXAMPLES_DIR="$2"; shift 2 ;;
    --api) API_BASE="$2"; shift 2 ;;
    -h|--help)
      sed -n '2,12p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

API_BASE="${API_BASE%/}"
ACTUALS_FILE="$EXAMPLES_DIR/actuals-summary-utility-oct-2026.json"
EVENTS_FILE="$EXAMPLES_DIR/operational-events-oct-2026-checks.json"

die() { echo "error: $*" >&2; exit 1; }

[[ -f "$ACTUALS_FILE" ]] || die "missing $ACTUALS_FILE"
[[ -f "$EVENTS_FILE" ]] || die "missing $EVENTS_FILE"
command -v curl >/dev/null || die "curl is required"
command -v jq >/dev/null || die "jq is required (to unwrap PayloadBatch.items for events)"

if [[ "$VALIDATE" -eq 1 ]]; then
  if command -v npx >/dev/null; then
    echo "Validating examples with ajv (draft 2020-12)…"
    npx --yes ajv-cli@5 validate -s "$SCHEMAS_DIR/actuals-summary.schema.json" -d "$ACTUALS_FILE" --spec=draft2020 || die "actuals schema validation failed"
    # Events file is a PayloadBatch envelope; validate items individually against operational-event schema.
    TMP_ITEMS="$(mktemp)"
    jq -c '.items[]' "$EVENTS_FILE" > "$TMP_ITEMS"
    while IFS= read -r line; do
      echo "$line" | npx --yes ajv-cli@5 validate -s "$SCHEMAS_DIR/operational-event.schema.json" --spec=draft2020 || die "event item schema validation failed"
    done < "$TMP_ITEMS"
    rm -f "$TMP_ITEMS"
    echo "Schema validation OK."
  else
    echo "warn: --validate requested but npx not found; skipping schema check." >&2
  fi
fi

echo "POST $API_BASE/api/reference-data/actuals ← $(basename "$ACTUALS_FILE")"
ACTUALS_RESP="$(curl -sS -w "\n%{http_code}" -X POST "$API_BASE/api/reference-data/actuals" \
  -H "Content-Type: application/json" \
  --data-binary @"$ACTUALS_FILE")"
ACTUALS_BODY="$(echo "$ACTUALS_RESP" | sed '$d')"
ACTUALS_CODE="$(echo "$ACTUALS_RESP" | tail -n1)"
[[ "$ACTUALS_CODE" == "202" ]] || die "actuals POST expected 202, got $ACTUALS_CODE: $ACTUALS_BODY"
echo "  → $ACTUALS_BODY"

EVENTS_ITEMS="$(jq -c '.items' "$EVENTS_FILE")"
[[ "$(echo "$EVENTS_ITEMS" | jq 'length')" -gt 0 ]] || die "events file has empty items[]"

echo "POST $API_BASE/api/reference-data/events ← $(basename "$EVENTS_FILE") (items only)"
EVENTS_RESP="$(curl -sS -w "\n%{http_code}" -X POST "$API_BASE/api/reference-data/events" \
  -H "Content-Type: application/json" \
  --data-binary "$EVENTS_ITEMS")"
EVENTS_BODY="$(echo "$EVENTS_RESP" | sed '$d')"
EVENTS_CODE="$(echo "$EVENTS_RESP" | tail -n1)"
[[ "$EVENTS_CODE" == "202" ]] || die "events POST expected 202, got $EVENTS_CODE: $EVENTS_BODY"
echo "  → $EVENTS_BODY"

echo
echo "Done. Refresh Town Hub or /wiley-workspace/reference-data to see tiles/grids."
echo "GET $API_BASE/api/reference-data/actuals"
echo "GET $API_BASE/api/reference-data/events"
