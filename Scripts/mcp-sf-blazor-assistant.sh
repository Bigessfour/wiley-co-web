#!/usr/bin/env bash
# Launcher for Syncfusion Blazor MCP (@syncfusion/blazor-assistant).
# Fills SYNCFUSION_API_KEY from macOS Keychain (Passwords / Keychain Access) then execs npx.
#
# Preferred: generic password with Service Name (Keychain “Name”) exactly:
#   com.wileyco.syncfusion.blazor-mcp  and  Account = your macOS username ($USER)
#
# One-time (or duplicate the value you stored in Passwords):
#   security add-generic-password -a "$USER" -s "com.wileyco.syncfusion.blazor-mcp" -U -w "YOUR_API_KEY"
#
# If you saved under a different label in Passwords, open Keychain Access → search “Syncfusion” →
# double-click → note “Name” (service) or set the item’s label, then either:
#   export SYNCFUSION_KEYCHAIN_SERVICE="exact-name-field"
#   export SYNCFUSION_KEYCHAIN_ACCOUNT="exact-account-field"   # often email, not $USER
# or:
#   export SYNCFUSION_KEYCHAIN_LABEL="exact-label-field"
#
# If SYNCFUSION_API_KEY is already set in the environment, keychain lookup is skipped.

set -euo pipefail

SERVICE="${SYNCFUSION_KEYCHAIN_SERVICE:-com.wileyco.syncfusion.blazor-mcp}"
ACCOUNT="${SYNCFUSION_KEYCHAIN_ACCOUNT:-$USER}"

get_genpw() {
	security find-generic-password -w "$@" 2>/dev/null || true
}

if [[ -z ${SYNCFUSION_API_KEY:-} ]]; then
	KEY="$(get_genpw -s "$SERVICE" -a "$ACCOUNT")"
	if [[ -z $KEY && -n ${SYNCFUSION_KEYCHAIN_LABEL:-} ]]; then
		KEY="$(get_genpw -l "$SYNCFUSION_KEYCHAIN_LABEL")"
	fi
	if [[ -z $KEY ]]; then
		KEY="$(get_genpw -l "SYNCFUSION_API_KEY")"
	fi
	if [[ -z $KEY ]]; then
		echo "mcp-sf-blazor-assistant: could not read API key from Keychain." >&2
		echo 'Passwords often uses a different "Name"/account than this script’s default.' >&2
		echo "Fix (pick one):" >&2
		echo "  1) security add-generic-password -a \"\$USER\" -s \"${SERVICE}\" -U -w \"(paste key)\"" >&2
		echo "  2) Set SYNCFUSION_KEYCHAIN_SERVICE / SYNCFUSION_KEYCHAIN_ACCOUNT to match Keychain Access" >&2
		echo "  3) Set SYNCFUSION_KEYCHAIN_LABEL to the item’s Label, or name an item label: SYNCFUSION_API_KEY" >&2
		exit 1
	fi
	export SYNCFUSION_API_KEY="$KEY"
fi

exec npx -y @syncfusion/blazor-assistant@latest "$@"
