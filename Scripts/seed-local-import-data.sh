#!/usr/bin/env bash
set -euo pipefail

API_BASE_URL="${API_BASE_URL:-http://localhost:5231}"
IMPORT_DATA_PATH="${IMPORT_DATA_PATH:-$(cd "$(dirname "$0")/../Import Data" && pwd)}"

if [[ ! -d $IMPORT_DATA_PATH ]]; then
	echo "Import data folder not found: $IMPORT_DATA_PATH" >&2
	exit 1
fi

payload=$(
	cat <<EOF
{
  "importDataPath": "$IMPORT_DATA_PATH",
  "includeSampleLedgerData": true,
  "applyDefaultEnterpriseBaselines": true
}
EOF
)

echo "Seeding workspace reference data from: $IMPORT_DATA_PATH"
curl -fsS -X POST "$API_BASE_URL/api/workspace/reference-data/import" \
	-H "Content-Type: application/json" \
	-d "$payload"

echo ""
echo "Seed request completed."
