#!/usr/bin/env bash
# Copy clerk check-listing workbooks, and any ledger spreadsheets in Deb's PC
# backup, then send new checks through the existing QuickBooks import.
# A run with no arguments only reports what would be imported.
# Pass --commit to write into wileyco_dev. The company file is not read.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
STATE_DIR="${WILEY_CLERK_STATE_DIR:-/tmp/wiley-clerk-local}"
STAGING="$STATE_DIR/deb-backup-imports"
STAMP="$STATE_DIR/deb-backup.stamp"
API="${WILEY_WORKSPACE_API_BASE_ADDRESS:-http://127.0.0.1:5231}"
NAS_HOST="${WILEY_BACKUP_SSH_HOST:-mr-storage}"
BACKUP_ROOT="${WILEY_DEBBIE_BACKUP_ROOT:-/volume1/homes/deb/Backup/DESKTOP-KN6INHL}"
LISTING_ROOT="${WILEY_CHECK_LISTING_ROOT:-/volume1/Town of Wiley Shared Documents/CHECK LISTING/2026 CHECK LISTING}"
LISTING_STAGE="$STAGING/check-listing"
COMMIT=0
if [[ "${1:-}" == "--commit" ]]; then
  COMMIT=1
fi

log() { printf '[import-deb-backup] %s\n' "$*"; }
die() { printf '[import-deb-backup] ERROR: %s\n' "$*" >&2; exit 1; }

if [[ "${1:-}" == "--install" ]]; then
  PLIST="$HOME/Library/LaunchAgents/com.townofwiley.deb-backup-import.plist"
  mkdir -p "$HOME/Library/LaunchAgents" "$STATE_DIR"
  cat >"$PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>com.townofwiley.deb-backup-import</string>
  <key>ProgramArguments</key>
  <array>
    <string>${ROOT}/Scripts/import-deb-backup.sh</string>
  </array>
  <key>StartInterval</key>
  <integer>900</integer>
  <key>RunAtLoad</key>
  <false/>
  <key>StandardOutPath</key>
  <string>${STATE_DIR}/deb-backup-import.log</string>
  <key>StandardErrorPath</key>
  <string>${STATE_DIR}/deb-backup-import.log</string>
</dict>
</plist>
EOF
  launchctl bootout "gui/$(id -u)" "$PLIST" 2>/dev/null || true
  launchctl bootstrap "gui/$(id -u)" "$PLIST"
  log "Installed. Every 15 minutes it reports new check listings. It does not write to the database, and it does not run at login."
  exit 0
fi

mkdir -p "$STAGING" "$LISTING_STAGE" "$STATE_DIR"

read_mtime() {
  ssh -o ConnectTimeout=8 -o BatchMode=yes "$NAS_HOST" python3 - "$1" <<'PY'
import os, sys
path = sys.argv[1]
if not os.path.isdir(path):
    raise SystemExit("missing folder: " + path)
print(int(os.stat(path).st_mtime))
PY
}

listing_mtime="$(read_mtime "$LISTING_ROOT")" || die "cannot read check listings on $NAS_HOST:$LISTING_ROOT"
backup_mtime="$(read_mtime "$BACKUP_ROOT")" || die "cannot read backup time on $NAS_HOST:$BACKUP_ROOT"
remote_stamp="${listing_mtime}:${backup_mtime}"

previous="0"
if [[ -f "$STAMP" ]]; then
  previous="$(cat "$STAMP")"
fi

if [[ "$COMMIT" == "1" && "$remote_stamp" == "$previous" ]]; then
  log "No new check listing or backup. Last seen $remote_stamp."
  exit 0
fi

log "Copying 2026 check-listing workbooks..."
rm -rf "$LISTING_STAGE"
mkdir -p "$LISTING_STAGE"
ssh -o ConnectTimeout=8 -o BatchMode=yes "$NAS_HOST" \
  "tar -C $(printf '%q' "$LISTING_ROOT") -cf - --exclude='@eaDir' --exclude='#recycle' ." \
  | tar -C "$LISTING_STAGE" -xf -

log "Copying spreadsheets from Deb's PC backup..."
pc_list="$STATE_DIR/pc-backup-files.txt"
ssh -o ConnectTimeout=8 -o BatchMode=yes "$NAS_HOST" python3 - "$BACKUP_ROOT" <<'PY' >"$pc_list"
import os, sys
root = sys.argv[1]
skip = {"appdata", "windows", "program files", "program files (x86)", "downloads", "#recycle", "@eadir"}
exts = {".csv", ".xlsx", ".xls"}
for dirpath, dirnames, filenames in os.walk(root):
    dirnames[:] = [d for d in dirnames if d.lower() not in skip]
    for name in filenames:
        if os.path.splitext(name)[1].lower() in exts:
            print(os.path.relpath(os.path.join(dirpath, name), root))
PY
if [[ -s "$pc_list" ]]; then
  mkdir -p "$STAGING/pc-backup"
  ssh -o ConnectTimeout=8 -o BatchMode=yes "$NAS_HOST" \
    "tar -C $(printf '%q' "$BACKUP_ROOT") -cf - -T -" <"$pc_list" \
    | tar -C "$STAGING/pc-backup" -xf -
else
  log "No ledger spreadsheets in Deb's PC backup."
fi

if ! curl -sf "$API/health" >/dev/null; then
  if [[ "$COMMIT" == "1" ]]; then
    die "API is not healthy at $API. Start it with ./Scripts/start-clerk-local.sh"
  fi
  log "Files are staged in $STAGING. The API is not running, so nothing was counted or written."
  exit 0
fi

if [[ "$COMMIT" == "1" ]]; then
  log "Sending new checks through /api/imports/quickbooks/from-backup?commit=true"
  curl -sS -f -X POST "$API/api/imports/quickbooks/from-backup?commit=true" \
    -H 'Content-Type: application/json' \
    -d '{}' | python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("message")); print("imported", d.get("importedFiles"), "duplicates", d.get("duplicateFiles"), "skipped", d.get("skippedFiles"))'
  printf '%s\n' "$remote_stamp" >"$STAMP"
  log "Recorded backup time $remote_stamp"
else
  log "Dry run. Nothing will be written."
  curl -sS -f -X POST "$API/api/imports/quickbooks/from-backup" \
    -H 'Content-Type: application/json' \
    -d '{}' | python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("message"));
ready=[f for f in d.get("files") or [] if f.get("status")=="ready" and (f.get("importedRows") or 0)>0]
print("ready files", len(ready))
for f in ready:
    print(f.get("importedRows"), f.get("enterprise"), f.get("relativePath"))'
fi
