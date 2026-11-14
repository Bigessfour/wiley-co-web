#!/usr/bin/env bash
# Copy clerk check-listing workbooks, and any ledger spreadsheets in Deb's PC
# backup, then send new checks through the existing QuickBooks import.
# A run with no arguments only reports what would be imported.
# Pass --commit to print that report, then write into wileyco_dev.
# The stamp is a hash of each workbook's path, size, and time, not the
# parent folder's time. It is written only when every file succeeds.
# The company file is not read.
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

# OpenSSH joins the remote command and the remote shell splits it again.
# Quote the path for that shell. A space in "Town of Wiley" must stay one argument.
ssh_python() {
  local quoted
  quoted="$(printf '%q' "$1")"
  ssh -o ConnectTimeout=8 -o BatchMode=yes "$NAS_HOST" "python3 - $quoted"
}

read_manifest() {
  ssh_python "$1" <<'PY'
import hashlib, os, sys
root = sys.argv[1]
if not os.path.isdir(root):
    raise SystemExit("missing folder: " + root)
skip = {"appdata", "windows", "program files", "program files (x86)", "downloads", "#recycle", "@eadir"}
exts = {".csv", ".xlsx", ".xls"}
lines = []
for dirpath, dirnames, filenames in os.walk(root):
    dirnames[:] = [name for name in dirnames if name.lower() not in skip]
    for name in filenames:
        if os.path.splitext(name)[1].lower() not in exts:
            continue
        full = os.path.join(dirpath, name)
        rel = os.path.relpath(full, root).replace("\\", "/")
        st = os.stat(full)
        lines.append(f"{rel}\t{st.st_size}\t{st.st_mtime_ns}")
digest = hashlib.sha256("\n".join(sorted(lines)).encode()).hexdigest()
print(f"{digest}:{len(lines)}")
PY
}

listing_manifest="$(read_manifest "$LISTING_ROOT")" || die "cannot read check listings on $NAS_HOST:$LISTING_ROOT"
backup_manifest="$(read_manifest "$BACKUP_ROOT")" || die "cannot read backup files on $NAS_HOST:$BACKUP_ROOT"
remote_stamp="${listing_manifest}:${backup_manifest}"

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
ssh_python "$BACKUP_ROOT" <<'PY' >"$pc_list"
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

post_backup() {
  local query="$1"
  local mode="$2"
  local body="$STATE_DIR/deb-backup-response.json"
  local code
  code="$(curl -sS -o "$body" -w '%{http_code}' -X POST "$API/api/imports/quickbooks/from-backup${query}" \
    -H 'Content-Type: application/json' \
    -d '{}')"
  if [[ "$code" != "200" ]]; then
    die "Backup import returned HTTP ${code}. $(head -c 400 "$body" 2>/dev/null || true)"
  fi
  python3 - "$body" "$mode" <<'PY'
import json, sys
path, mode = sys.argv[1], sys.argv[2]
with open(path, encoding="utf-8") as handle:
    report = json.load(handle)
print(report.get("message") or "")
files = report.get("files") or []
failed = [item for item in files if item.get("status") == "failed"]
ready = [item for item in files if item.get("status") == "ready" and (item.get("importedRows") or 0) > 0]
if mode == "preview":
    print("ready files", len(ready))
    for item in ready:
        print(item.get("importedRows"), item.get("enterprise"), item.get("relativePath"))
    if failed:
        print("failed files", len(failed))
        for item in failed:
            print("FAILED", item.get("relativePath"), item.get("message"))
else:
    print("imported", report.get("importedFiles"), "duplicates", report.get("duplicateFiles"), "skipped", report.get("skippedFiles"))
    if failed:
        print("failed files", len(failed))
        for item in failed:
            print("FAILED", item.get("relativePath"), item.get("message"))
        raise SystemExit(1)
PY
}

if [[ "$COMMIT" == "1" ]]; then
  log "Preview of what --commit will write. Nothing is written by this call."
  post_backup "" "preview"
  log "Sending new checks through /api/imports/quickbooks/from-backup?commit=true"
  post_backup "?commit=true" "commit"
  printf '%s\n' "$remote_stamp" >"$STAMP"
  log "Recorded backup manifest."
else
  log "Dry run. Nothing will be written."
  post_backup "" "preview"
fi
