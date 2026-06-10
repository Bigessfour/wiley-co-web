#!/usr/bin/env bash
# Read or write the Syncfusion Blazor LICENSE key via macOS Keychain / Passwords.
#
# Store (from repo-root appsettings.Syncfusion.local.json):
#   ./Scripts/syncfusion-license-keychain.sh store
#
# Export for current shell / launchctl (metadata only on stdout for export):
#   eval "$(./Scripts/syncfusion-license-keychain.sh export)"
#   ./Scripts/syncfusion-license-keychain.sh launchctl
#
# Keychain item (Passwords app):
#   Name (service): com.wileyco.syncfusion.license
#   Account:        $USER
#   Label:          SYNCFUSION_LICENSE_KEY
#
# This is the Blazor license key — not the MCP API key (com.wileyco.syncfusion.blazor-mcp).

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_JSON="$ROOT_DIR/appsettings.Syncfusion.local.json"
SERVICE="${SYNCFUSION_LICENSE_KEYCHAIN_SERVICE:-com.wileyco.syncfusion.license}"
ACCOUNT="${SYNCFUSION_LICENSE_KEYCHAIN_ACCOUNT:-$USER}"
LABEL="${SYNCFUSION_LICENSE_KEYCHAIN_LABEL:-SYNCFUSION_LICENSE_KEY}"

read_key_from_json() {
	if [[ ! -f $LOCAL_JSON ]]; then
		echo "syncfusion-license-keychain: missing $LOCAL_JSON" >&2
		return 1
	fi
	node -e "
const fs = require('fs');
const path = process.argv[1];
const raw = fs.readFileSync(path, 'utf8');
const parsed = JSON.parse(raw);
const key = (parsed.SyncfusionLicenseKey || parsed.SYNCFUSION_LICENSE_KEY || '').trim();
if (!key) { process.exit(2); }
process.stdout.write(key);
" "$LOCAL_JSON"
}

read_key_from_keychain() {
	security find-generic-password -w -s "$SERVICE" -a "$ACCOUNT" 2>/dev/null ||
		security find-generic-password -w -l "$LABEL" 2>/dev/null ||
		true
}

store_key_in_keychain() {
	local key="$1"
	security add-generic-password \
		-a "$ACCOUNT" \
		-s "$SERVICE" \
		-l "$LABEL" \
		-U \
		-w "$key" >/dev/null
}

cmd="${1:-export}"

case "$cmd" in
store)
	KEY="$(read_key_from_json)"
	store_key_in_keychain "$KEY"
	echo "Stored Syncfusion license in Passwords/Keychain (service=$SERVICE, length=${#KEY})."
	;;
export)
	KEY="$(read_key_from_keychain)"
	if [[ -z $KEY ]]; then
		KEY="$(read_key_from_json)" || true
	fi
	if [[ -z $KEY ]]; then
		echo "echo 'syncfusion-license-keychain: no key in keychain or $LOCAL_JSON' >&2" >&2
		exit 1
	fi
	printf 'export SYNCFUSION_LICENSE_KEY=%q\n' "$KEY"
	;;
launchctl)
	KEY="$(read_key_from_keychain)"
	if [[ -z $KEY ]]; then
		KEY="$(read_key_from_json)"
		store_key_in_keychain "$KEY"
	fi
	launchctl setenv SYNCFUSION_LICENSE_KEY "$KEY"
	echo "launchctl SYNCFUSION_LICENSE_KEY set (length=${#KEY}). Restart Terminal/Cursor to inherit in new GUI sessions."
	;;
verify)
	KEY="$(read_key_from_keychain)"
	JSON_KEY="$(read_key_from_json 2>/dev/null || true)"
	if [[ -z $KEY ]]; then
		echo "keychain: missing"
	else
		echo "keychain: present length=${#KEY}"
	fi
	if [[ -z $JSON_KEY ]]; then
		echo "local-json: missing or invalid"
	else
		echo "local-json: present length=${#JSON_KEY}"
	fi
	if [[ -n $KEY && -n $JSON_KEY && $KEY != "$JSON_KEY" ]]; then
		echo "warning: keychain and appsettings.Syncfusion.local.json differ"
		exit 2
	fi
	if [[ -z $KEY && -z $JSON_KEY ]]; then
		exit 1
	fi
	echo "ok"
	;;
*)
	echo "Usage: $0 {store|export|launchctl|verify}" >&2
	exit 1
	;;
esac
