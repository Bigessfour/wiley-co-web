#!/usr/bin/env bash
# Round-trip verify shared-contracts reference-data API with October 2026 TOW numbers.
# Builds a realistic ActualsSummary + 4 OperationalEvents (hard-coded), POSTs, GETs, asserts.
#
# Usage (API must be listening — Development auth off):
#   ./Scripts/verify-shared-contracts.sh
#   WILEY_WORKSPACE_API_BASE_ADDRESS=http://127.0.0.1:5231 ./Scripts/verify-shared-contracts.sh
#   ./Scripts/verify-shared-contracts.sh --api http://127.0.0.1:5231
#
# Exits non-zero if POST/GET fails or returned values do not match expected Oct 2026 figures.
set -euo pipefail

API_BASE="${WILEY_WORKSPACE_API_BASE_ADDRESS:-http://127.0.0.1:5231}"

while [[ $# -gt 0 ]]; do
  case "$1" in
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
die() { echo "FAIL: $*" >&2; exit 1; }

command -v curl >/dev/null || die "curl is required"
command -v jq >/dev/null || die "jq is required"

# October 2026 Town of Wiley — utility budget comparison + bank balances (council packet).
EXPECTED_INCOME="259177.18"
EXPECTED_EXPENSES="188843.30"
EXPECTED_CASH="152007.37"
EXPECTED_PERIOD="2026-10"
EXPECTED_ENTERPRISE="tow-water-utility"
EXPECTED_EVENT_COUNT=4

ACTUALS_JSON="$(jq -nc \
  --argjson income "$EXPECTED_INCOME" \
  --argjson expenses "$EXPECTED_EXPENSES" \
  --argjson cash "$EXPECTED_CASH" \
  --arg period "$EXPECTED_PERIOD" \
  --arg enterpriseId "$EXPECTED_ENTERPRISE" \
  '{
    schemaVersion: "1.0.0",
    enterpriseId: $enterpriseId,
    period: $period,
    amountsBasis: "fy-ytd",
    income: $income,
    expenses: $expenses,
    cashBalance: $cash,
    cashBalanceAccounts: ["UTILITY", "UTILITY SAVINGS"]
  }')"

# Four realistic OperationalEvents from Oct 2026 listings (utility + general + apartments + WSD).
EVENTS_JSON="$(jq -nc '
[
  {
    schemaVersion: "1.0.0",
    source: "council-packet",
    type: "check",
    date: "2026-10-06",
    amount: 344.84,
    description: "MAY VALLEY — HOTSPOT/CHLORINE",
    metadata: {
      enterpriseId: "tow-water-utility",
      checkNumber: "12378",
      payee: "MAY VALLEY",
      memo: "HOTSPOT/CHLORINE",
      account: "Utility:Water Supplies",
      listingFile: "utility-transactions-oct-2026.csv"
    }
  },
  {
    schemaVersion: "1.0.0",
    source: "council-packet",
    type: "check",
    date: "2026-10-08",
    amount: 592.91,
    description: "FAST STOP — FUEL",
    metadata: {
      enterpriseId: "tow-water-utility",
      checkNumber: "12379",
      payee: "FAST STOP",
      memo: "FUEL",
      account: "Utility:Fuel",
      listingFile: "utility-transactions-oct-2026.csv"
    }
  },
  {
    schemaVersion: "1.0.0",
    source: "council-packet",
    type: "check",
    date: "2026-10-06",
    amount: 396.00,
    description: "SE&EC RECYCLING — OCT-DEC",
    metadata: {
      enterpriseId: "tow-general",
      checkNumber: "17387",
      payee: "SE&EC RECYCLING",
      memo: "OCT-DEC",
      account: "General:Expenses",
      listingFile: "general-transactions-oct-2026.csv"
    }
  },
  {
    schemaVersion: "1.0.0",
    source: "council-packet",
    type: "summary",
    date: "2026-10-31",
    amount: 62008.71,
    description: "October 2026 check-listing grand total (Utility+General+Brookside+WSD)",
    metadata: {
      grandTotal: 62008.71,
      utilityTotal: 39077.79,
      generalTotal: 17189.56,
      brooksideTotal: 3757.99,
      wsdTotal: 1983.37,
      period: "2026-10"
    }
  }
]')"

echo "=== verify-shared-contracts ==="
echo "API: $API_BASE"

# Health probe (fail fast if API down)
HTTP_PROBE="$(curl -sS -o /dev/null -w "%{http_code}" "$API_BASE/api/reference-data/actuals" || true)"
[[ "$HTTP_PROBE" == "200" ]] || die "API not reachable at $API_BASE (GET /api/reference-data/actuals → HTTP $HTTP_PROBE). Start WileyCoWeb.Api on 5231 first."

echo "POST /api/reference-data/actuals (Oct 2026 utility ActualsSummary)…"
ACTUALS_RESP="$(curl -sS -w "\n%{http_code}" -X POST "$API_BASE/api/reference-data/actuals" \
  -H "Content-Type: application/json" \
  --data-binary "$ACTUALS_JSON")"
ACTUALS_BODY="$(printf '%s\n' "$ACTUALS_RESP" | sed '$d')"
ACTUALS_CODE="$(printf '%s\n' "$ACTUALS_RESP" | tail -n1)"
[[ "$ACTUALS_CODE" == "202" ]] || die "actuals POST expected 202, got $ACTUALS_CODE: $ACTUALS_BODY"
ACTUALS_ID="$(echo "$ACTUALS_BODY" | jq -r '.id // empty')"
[[ -n "$ACTUALS_ID" && "$ACTUALS_ID" != "null" ]] || die "actuals POST missing id: $ACTUALS_BODY"
echo "  accepted id=$ACTUALS_ID"

echo "POST /api/reference-data/events (4 OperationalEvents)…"
EVENTS_RESP="$(curl -sS -w "\n%{http_code}" -X POST "$API_BASE/api/reference-data/events" \
  -H "Content-Type: application/json" \
  --data-binary "$EVENTS_JSON")"
EVENTS_BODY="$(printf '%s\n' "$EVENTS_RESP" | sed '$d')"
EVENTS_CODE="$(printf '%s\n' "$EVENTS_RESP" | tail -n1)"
[[ "$EVENTS_CODE" == "202" ]] || die "events POST expected 202, got $EVENTS_CODE: $EVENTS_BODY"
EVENTS_ID="$(echo "$EVENTS_BODY" | jq -r '.id // empty')"
[[ -n "$EVENTS_ID" && "$EVENTS_ID" != "null" ]] || die "events POST missing id: $EVENTS_BODY"
echo "  accepted id=$EVENTS_ID"

echo "GET /api/reference-data/actuals/$ACTUALS_ID …"
GOT_ACTUALS="$(curl -sS -w "\n%{http_code}" "$API_BASE/api/reference-data/actuals/$ACTUALS_ID")"
GOT_ACTUALS_BODY="$(printf '%s\n' "$GOT_ACTUALS" | sed '$d')"
GOT_ACTUALS_CODE="$(printf '%s\n' "$GOT_ACTUALS" | tail -n1)"
[[ "$GOT_ACTUALS_CODE" == "200" ]] || die "actuals GET expected 200, got $GOT_ACTUALS_CODE: $GOT_ACTUALS_BODY"

GOT_INCOME="$(echo "$GOT_ACTUALS_BODY" | jq -r '.actuals.income')"
GOT_EXPENSES="$(echo "$GOT_ACTUALS_BODY" | jq -r '.actuals.expenses')"
GOT_CASH="$(echo "$GOT_ACTUALS_BODY" | jq -r '.actuals.cashBalance')"
GOT_PERIOD="$(echo "$GOT_ACTUALS_BODY" | jq -r '.actuals.period')"
GOT_ENT="$(echo "$GOT_ACTUALS_BODY" | jq -r '.actuals.enterpriseId')"

[[ "$GOT_PERIOD" == "$EXPECTED_PERIOD" ]] || die "period mismatch: $GOT_PERIOD"
[[ "$GOT_ENT" == "$EXPECTED_ENTERPRISE" ]] || die "enterpriseId mismatch: $GOT_ENT"
# Compare as numbers (jq)
echo "$GOT_ACTUALS_BODY" | jq -e \
  --argjson income "$EXPECTED_INCOME" \
  --argjson expenses "$EXPECTED_EXPENSES" \
  --argjson cash "$EXPECTED_CASH" \
  '.actuals.income == $income and .actuals.expenses == $expenses and .actuals.cashBalance == $cash' \
  >/dev/null \
  || die "actuals money fields mismatch (got income=$GOT_INCOME expenses=$GOT_EXPENSES cash=$GOT_CASH)"

echo "GET /api/reference-data/events/$EVENTS_ID …"
GOT_EVENTS="$(curl -sS -w "\n%{http_code}" "$API_BASE/api/reference-data/events/$EVENTS_ID")"
GOT_EVENTS_BODY="$(printf '%s\n' "$GOT_EVENTS" | sed '$d')"
GOT_EVENTS_CODE="$(printf '%s\n' "$GOT_EVENTS" | tail -n1)"
[[ "$GOT_EVENTS_CODE" == "200" ]] || die "events GET expected 200, got $GOT_EVENTS_CODE: $GOT_EVENTS_BODY"

EVENT_LEN="$(echo "$GOT_EVENTS_BODY" | jq '.events | length')"
[[ "$EVENT_LEN" -eq "$EXPECTED_EVENT_COUNT" ]] || die "expected $EXPECTED_EVENT_COUNT events, got $EVENT_LEN"

echo "$GOT_EVENTS_BODY" | jq -e \
  --argjson n "$EXPECTED_EVENT_COUNT" \
  '(.events | length) == $n
   and (.events | map(select(.type == "check")) | length) == 3
   and (.events | map(select(.type == "summary")) | length) == 1
   and ([.events[] | select(.type == "summary") | .metadata.grandTotal][0] == 62008.71)' \
  >/dev/null \
  || die "events payload shape/totals mismatch: $GOT_EVENTS_BODY"

cat <<EOF

OK — shared-contracts round-trip succeeded
  ActualsSummary  id=$ACTUALS_ID  period=$EXPECTED_PERIOD  income=$EXPECTED_INCOME  expenses=$EXPECTED_EXPENSES  cash=$EXPECTED_CASH
  OperationalEvents id=$EVENTS_ID  count=$EXPECTED_EVENT_COUNT  (3 checks + 1 summary grandTotal=62008.71)
EOF
