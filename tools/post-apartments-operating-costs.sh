#!/usr/bin/env bash
# First live publisher: pull OperationalEvent[] from Wiley Apartments (ClerkSuite)
# and POST to this Widget's reference-data API. Does not change Apartments or
# QuickBooks / snapshot / break-even behavior.
#
# Prefer this when both apps are local. Full mapping:
#   Wiley_Apartments/docs/shared-contracts-export.md
#
# Usage (from wiley-co-web/):
#   ./tools/post-apartments-operating-costs.sh
#   PERIOD=2026-10 ./tools/post-apartments-operating-costs.sh
#   APARTMENTS_BASE=http://127.0.0.1:5077 WIDGET_API=http://127.0.0.1:5231 ./tools/post-apartments-operating-costs.sh
set -euo pipefail

PERIOD="${PERIOD:-$(date -u +%Y-%m)}"
APARTMENTS_BASE="${APARTMENTS_BASE:-http://127.0.0.1:5077}"
WIDGET_API="${WIDGET_API:-${WILEY_WORKSPACE_API_BASE_ADDRESS:-http://127.0.0.1:5231}}"
APARTMENTS_BASE="${APARTMENTS_BASE%/}"
WIDGET_API="${WIDGET_API%/}"

command -v curl >/dev/null || { echo "curl required" >&2; exit 1; }
command -v jq >/dev/null || { echo "jq required" >&2; exit 1; }

EXPORT_URL="${APARTMENTS_BASE}/api/exports/shared-contracts/operational-events?period=${PERIOD}"

echo "=== apartments → widget reference-data ==="
echo "GET  ${EXPORT_URL}"
PAYLOAD="$(curl -sS -f "${EXPORT_URL}")" || {
  echo "FAIL: could not GET export (is ClerkSuite running on ${APARTMENTS_BASE}?)" >&2
  exit 1
}
COUNT="$(echo "${PAYLOAD}" | jq 'length')"
echo "  → ${COUNT} OperationalEvent(s)  (UnitOperatingCost → source=other type=check)"

if [[ "${COUNT}" -eq 0 ]]; then
  echo "Nothing to POST (no operating costs in ${PERIOD})."
  exit 0
fi

echo "POST ${WIDGET_API}/api/reference-data/events"
RESP="$(curl -sS -w "\n%{http_code}" -X POST "${WIDGET_API}/api/reference-data/events" \
  -H "Content-Type: application/json" \
  --data-binary "${PAYLOAD}")"
BODY="$(echo "${RESP}" | sed '$d')"
CODE="$(echo "${RESP}" | tail -n1)"
echo "  → HTTP ${CODE} ${BODY}"
[[ "${CODE}" == "202" ]] || exit 1
echo "OK — refresh Town Hub or /wiley-workspace/reference-data"
