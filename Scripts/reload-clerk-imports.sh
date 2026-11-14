#!/usr/bin/env bash
# Reload prepared FY2026 QuickBooks / reference-data into the local Widget API.
# Idempotent where the API duplicate guards apply; safe to re-run after a DB wipe.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
API="${WILEY_WORKSPACE_API_BASE_ADDRESS:-http://127.0.0.1:5231}"
IMPORT_ROOT="$ROOT/Import Data"
OUT="$IMPORT_ROOT/tow-october-2026"
YTD="$OUT/fy2026-ytd"
PY="${WILEY_PYTHON:-python3}"

log() { printf '[reload-clerk-imports] %s\n' "$*"; }
die() { printf '[reload-clerk-imports] ERROR: %s\n' "$*" >&2; exit 1; }

curl -sf "$API/health" >/dev/null || die "API not healthy at $API — run ./Scripts/start-clerk-local.sh first"

inject() {
  local file="$1" enterprise="$2"
  [[ -f "$file" ]] || die "missing $file"
  curl -sS -X POST "$API/api/imports/quickbooks/commit" \
    -F "file=@${file};type=text/csv" \
    -F "selectedEnterprise=${enterprise}" \
    -F "selectedFiscalYear=2026" | "$PY" -c 'import json,sys; d=json.load(sys.stdin); print((d.get("fileName") or "?")[:48], d.get("importedRows"), "dup="+str(d.get("isDuplicate")))'
}

write_nondup_from_preview() {
  local preview_json="$1" out_csv="$2"
  "$PY" - "$preview_json" "$out_csv" <<'PY'
import json, csv, sys
from pathlib import Path
d = json.load(open(sys.argv[1]))
rows = [r for r in (d.get("rows") or []) if not r.get("isDuplicate")]
out = Path(sys.argv[2])
out.parent.mkdir(parents=True, exist_ok=True)
fields = ["Date","Type","Num","Name","Memo","Account","Split","Amount","Balance","Clr"]
with out.open("w", newline="", encoding="utf-8") as f:
    w = csv.DictWriter(f, fieldnames=fields)
    w.writeheader()
    for r in rows:
        w.writerow({
            "Date": r.get("entryDate") or "",
            "Type": r.get("entryType") or "",
            "Num": r.get("transactionNumber") or "",
            "Name": r.get("name") or "",
            "Memo": r.get("memo") or "",
            "Account": r.get("accountName") or "",
            "Split": r.get("splitAccount") or "",
            "Amount": "" if r.get("amount") is None else r.get("amount"),
            "Balance": "" if r.get("runningBalance") is None else r.get("runningBalance"),
            "Clr": r.get("clearedFlag") or "",
        })
print(out.name, len(rows), "nondup rows")
PY
}

log "Reference-data import (customers + sample GLs)..."
curl -sS -X POST "$API/api/workspace/reference-data/import" \
  -H 'Content-Type: application/json' \
  -d "{\"importDataPath\":\"$IMPORT_ROOT\",\"includeSampleLedgerData\":true,\"applyDefaultEnterpriseBaselines\":true}" \
  | "$PY" -c 'import json,sys; d=json.load(sys.stdin); print("ref customers", d.get("importedUtilityCustomerCount"), "ledger", d.get("importedLedgerRowCount"))'

[[ -d "$YTD" ]] || die "missing $YTD — NAS FY2026 pack not present"

inject "$YTD/nas-utility-transactions-fy2026-ytd.csv" "Water Utility"
inject "$YTD/nas-trash-transactions-fy2026-ytd.csv" "Trash"
inject "$YTD/nas-wsd-transactions-fy2026-ytd.csv" "Wiley Sanitation District"
inject "$YTD/nas-brookside-transactions-fy2026-ytd.csv" "Apartments"

for f in "$YTD"/util-txn-chunks/util-txn-chunk-0{1,2,3,4}.csv; do
  [[ -f "$f" ]] && inject "$f" "Water Utility"
done

if [[ -f "$YTD/util-txn-chunks/util-txn-chunk-05.csv" ]]; then
  curl -sS -X POST "$API/api/imports/quickbooks/preview" \
    -F "file=@$YTD/util-txn-chunks/util-txn-chunk-05.csv;type=text/csv" \
    -F "selectedEnterprise=Water Utility" \
    -F "selectedFiscalYear=2026" >/tmp/wiley-c5-preview.json
  write_nondup_from_preview /tmp/wiley-c5-preview.json "$YTD/util-txn-chunks/util-txn-chunk-05-nondup.csv"
  inject "$YTD/util-txn-chunks/util-txn-chunk-05-nondup.csv" "Water Utility"
fi

if [[ -f "$IMPORT_ROOT/transaction-list-by-date-all-wsd.xlsx" ]]; then
  curl -sS -X POST "$API/api/imports/quickbooks/preview" \
    -F "file=@$IMPORT_ROOT/transaction-list-by-date-all-wsd.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
    -F "selectedEnterprise=Wiley Sanitation District" \
    -F "selectedFiscalYear=2026" >/tmp/wiley-wsd-preview.json
  write_nondup_from_preview /tmp/wiley-wsd-preview.json "$YTD/wsd-txn-nondup.csv"
  inject "$YTD/wsd-txn-nondup.csv" "Wiley Sanitation District"
fi

[[ -f "$OUT/wsd-enterprise-transactions-oct-2026.csv" ]] && inject "$OUT/wsd-enterprise-transactions-oct-2026.csv" "Wiley Sanitation District" || true
[[ -f "$OUT/wsd-general-transactions-oct-2026.csv" ]] && inject "$OUT/wsd-general-transactions-oct-2026.csv" "Wiley Sanitation District" || true
[[ -f "$OUT/utility-transactions-oct-2026-starter.csv" ]] && inject "$OUT/utility-transactions-oct-2026-starter.csv" "Trash" || true

curl -sS "$API/api/imports/quickbooks/history" | "$PY" -c '
import json,sys
items=json.load(sys.stdin).get("items",[])
print(len(items), "batches", sum(i.get("rowCount") or 0 for i in items), "rows")
by={}
for i in items:
  by[i.get("scopeSummary")]=by.get(i.get("scopeSummary"),0)+(i.get("rowCount") or 0)
print(by)
'
log "Done."
