#!/usr/bin/env bash
# Read or write the Syncfusion Blazor MCP API key via macOS Keychain / Passwords.
#
# Store from an existing key file or stdin:
#   ./Scripts/syncfusion-api-keychain.sh store
#   cat ~/.config/syncfusion/documentsdk.key | ./Scripts/syncfusion-api-keychain.sh store-stdin
#
# Export for current shell (metadata only on stdout for export):
#   eval "$(./Scripts/syncfusion-api-keychain.sh export)"
#   ./Scripts/syncfusion-api-keychain.sh launchctl
#
# Sync keychain → ~/.config/syncfusion/documentsdk.key (MCP Syncfusion_API_Key_Path):
#   ./Scripts/syncfusion-api-keychain.sh sync-path
#
# Keychain item (Passwords app):
#   Name (service): com.wileyco.syncfusion.blazor-mcp
#   Account:        $USER
#   Label:          SYNCFUSION_API_KEY
#
# This is the MCP / documentation API key — not the Blazor runtime license
# (see Scripts/syncfusion-license-keychain.sh).

set -euo pipefail

SERVICE="${SYNCFUSION_KEYCHAIN_SERVICE:-com.wileyco.syncfusion.blazor-mcp}"
ACCOUNT="${SYNCFUSION_KEYCHAIN_ACCOUNT:-$USER}"
LABEL="${SYNCFUSION_KEYCHAIN_LABEL:-SYNCFUSION_API_KEY}"
API_KEY_PATH="${SYNCFUSION_API_KEY_PATH:-$HOME/.config/syncfusion/documentsdk.key}"

read_key_from_path() {
	if [[ ! -f $API_KEY_PATH ]]; then
		return 1
	fi
	tr -d '[:space:]' <"$API_KEY_PATH"
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

write_key_to_path() {
	local key="$1"
	mkdir -p "$(dirname "$API_KEY_PATH")"
	printf '%s' "$key" >"$API_KEY_PATH"
	chmod 600 "$API_KEY_PATH"
}

export_env_lines() {
	local key="$1"
	printf 'export SYNCFUSION_API_KEY=%q\n' "$key"
	printf 'export Syncfusion_API_Key=%q\n' "$key"
	printf 'export Syncfusion_API_Key_Path=%q\n' "$API_KEY_PATH"
	printf 'export SYNCFUSION_API_KEY_PATH=%q\n' "$API_KEY_PATH"
}

cmd="${1:-export}"

case "$cmd" in
store)
	KEY="$(read_key_from_path)" || {
		echo "syncfusion-api-keychain: missing $API_KEY_PATH" >&2
		exit 1
	}
	store_key_in_keychain "$KEY"
	echo "Stored Syncfusion MCP API key in Passwords/Keychain (service=$SERVICE, length=${#KEY})."
	;;
store-stdin)
	KEY="$(tr -d '[:space:]')"
	if [[ -z $KEY ]]; then
		echo "syncfusion-api-keychain: empty stdin" >&2
		exit 1
	fi
	store_key_in_keychain "$KEY"
	write_key_to_path "$KEY"
	echo "Stored Syncfusion MCP API key from stdin (length=${#KEY})."
	;;
export)
	KEY="$(read_key_from_keychain)"
	if [[ -z $KEY ]]; then
		KEY="$(read_key_from_path)" || true
	fi
	if [[ -z $KEY ]]; then
		echo "echo 'syncfusion-api-keychain: no key in keychain or $API_KEY_PATH' >&2" >&2
		exit 1
	fi
	write_key_to_path "$KEY"
	export_env_lines "$KEY"
	;;
launchctl)
	KEY="$(read_key_from_keychain)"
	if [[ -z $KEY ]]; then
		KEY="$(read_key_from_path)" || {
			echo "syncfusion-api-keychain: no key to publish" >&2
			exit 1
		}
		store_key_in_keychain "$KEY"
	fi
	write_key_to_path "$KEY"
	launchctl setenv SYNCFUSION_API_KEY "$KEY"
	launchctl setenv Syncfusion_API_Key "$KEY"
	launchctl setenv Syncfusion_API_Key_Path "$API_KEY_PATH"
	launchctl setenv SYNCFUSION_API_KEY_PATH "$API_KEY_PATH"
	echo "launchctl Syncfusion MCP env set (length=${#KEY}, path=$API_KEY_PATH)."
	echo "Restart Cursor so GUI-launched MCP servers inherit launchctl values."
	;;
sync-path)
	KEY="$(read_key_from_keychain)"
	if [[ -z $KEY ]]; then
		echo "syncfusion-api-keychain: keychain missing; nothing to sync" >&2
		exit 1
	fi
	write_key_to_path "$KEY"
	echo "Synced keychain → $API_KEY_PATH (length=${#KEY})."
	;;
verify)
	KEY="$(read_key_from_keychain)"
	FILE_KEY="$(read_key_from_path 2>/dev/null || true)"
	if [[ -z $KEY ]]; then
		echo "keychain: missing"
	else
		echo "keychain: present length=${#KEY}"
	fi
	if [[ -z $FILE_KEY ]]; then
		echo "path-file: missing or invalid ($API_KEY_PATH)"
	else
		echo "path-file: present length=${#FILE_KEY}"
	fi
	if [[ -n $KEY && -n $FILE_KEY && $KEY != "$FILE_KEY" ]]; then
		echo "warning: keychain and path file differ"
		exit 2
	fi
	if [[ -z $KEY && -z $FILE_KEY ]]; then
		exit 1
	fi
	echo "ok"
	;;
*)
	echo "Usage: $0 {store|store-stdin|export|launchctl|sync-path|verify}" >&2
	exit 1
	;;
esac
