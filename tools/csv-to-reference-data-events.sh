#!/usr/bin/env bash
# Offline adapter: map a simple apartments-style operating-cost CSV → OperationalEvent[]
# and POST to Widget /api/reference-data/events. Use when ClerkSuite is not running.
#
# CSV columns (header required): date,amount,vendor,category,unitNumber,notes
# Mapping (shared-contracts v1 closed enums — there is no source=trash-truck or type=cost):
#   source              = other
#   type                = check          (positive outflow)
#   metadata.enterpriseId     = tow-apartments
#   metadata.publisherSystem  = wiley-apartments-csv
#   metadata.costCategory     = category column
#
# Usage:
#   ./tools/csv-to-reference-data-events.sh
#   ./tools/csv-to-reference-data-events.sh tools/sample-apartments-operating-costs.csv
#   WIDGET_API=http://127.0.0.1:5231 ./tools/csv-to-reference-data-events.sh path.csv
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CSV="${1:-$ROOT/tools/sample-apartments-operating-costs.csv}"
WIDGET_API="${WIDGET_API:-${WILEY_WORKSPACE_API_BASE_ADDRESS:-http://127.0.0.1:5231}}"
WIDGET_API="${WIDGET_API%/}"

command -v curl >/dev/null || { echo "curl required" >&2; exit 1; }
command -v jq >/dev/null || { echo "jq required" >&2; exit 1; }
[[ -f "$CSV" ]] || { echo "missing CSV: $CSV" >&2; exit 1; }

# Skip header; build JSON array.
PAYLOAD="$(
  tail -n +2 "$CSV" | awk -F',' '
    NF >= 2 && $1 != "" {
      date=$1; amount=$2; vendor=$3; category=$4; unit=$5;
      notes=$6;
      for (i=7; i<=NF; i++) notes=notes "," $i
      gsub(/\r/, "", notes)
      printf "%s\t%s\t%s\t%s\t%s\t%s\n", date, amount, vendor, category, unit, notes
    }
  ' | jq -R -s -c '
    split("\n")
    | map(select(length > 0))
    | map(split("\t"))
    | map(select(length >= 2))
    | map({
        schemaVersion: "1.0.0",
        source: "other",
        type: "check",
        date: .[0],
        amount: (.[1] | tonumber),
        description: (
          (if .[2] == "" then .[3] else (.[2] + " — " + .[3]) end)
          + (if .[4] == "" then "" else (" (Unit " + .[4] + ")") end)
          + (if .[5] == "" then "" else (": " + .[5]) end)
        ),
        metadata: {
          enterpriseId: "tow-apartments",
          publisherSystem: "wiley-apartments-csv",
          costCategory: .[3],
          unitNumber: (if .[4] == "" then null else .[4] end),
          vendor: (if .[2] == "" then null else .[2] end),
          payee: (if .[2] == "" then null else .[2] end)
        }
      })
  '
)"

COUNT="$(echo "$PAYLOAD" | jq 'length')"
echo "=== csv → widget reference-data ==="
echo "CSV  $CSV → ${COUNT} OperationalEvent(s)"
[[ "$COUNT" -gt 0 ]] || { echo "No rows"; exit 1; }

echo "POST ${WIDGET_API}/api/reference-data/events"
RESP="$(curl -sS -w "\n%{http_code}" -X POST "${WIDGET_API}/api/reference-data/events" \
  -H "Content-Type: application/json" \
  --data-binary "${PAYLOAD}")"
BODY="$(echo "${RESP}" | sed '$d')"
CODE="$(echo "${RESP}" | tail -n1)"
echo "  → HTTP ${CODE} ${BODY}"
[[ "${CODE}" == "202" ]] || exit 1
echo "OK"
