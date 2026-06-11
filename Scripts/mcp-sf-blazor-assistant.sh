#!/usr/bin/env bash
# Launcher for Syncfusion Blazor MCP (@syncfusion/blazor-assistant).
# Reads the MCP API key from macOS Passwords/Keychain via syncfusion-api-keychain.sh,
# exports every env name the assistant expects, then execs npx.
#
# One-time setup:
#   ./Scripts/syncfusion-api-keychain.sh launchctl
#   ./Scripts/syncfusion-api-keychain.sh verify
#
# The @syncfusion/blazor-assistant package reads Syncfusion_API_Key or
# Syncfusion_API_Key_Path (not SYNCFUSION_API_KEY alone).

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
KEYCHAIN_SCRIPT="$ROOT_DIR/Scripts/syncfusion-api-keychain.sh"

if [[ ! -x $KEYCHAIN_SCRIPT ]]; then
	chmod +x "$KEYCHAIN_SCRIPT" 2>/dev/null || true
fi

if [[ -z ${Syncfusion_API_Key:-} && -z ${SYNCFUSION_API_KEY:-} ]]; then
	# shellcheck disable=SC1090
	eval "$("$KEYCHAIN_SCRIPT" export)"
elif [[ -n ${SYNCFUSION_API_KEY:-} && -z ${Syncfusion_API_Key:-} ]]; then
	export Syncfusion_API_Key="$SYNCFUSION_API_KEY"
	export Syncfusion_API_Key_Path="${Syncfusion_API_Key_Path:-${SYNCFUSION_API_KEY_PATH:-$HOME/.config/syncfusion/documentsdk.key}}"
elif [[ -n ${Syncfusion_API_Key:-} && -z ${SYNCFUSION_API_KEY:-} ]]; then
	export SYNCFUSION_API_KEY="$Syncfusion_API_Key"
	export Syncfusion_API_Key_Path="${Syncfusion_API_Key_Path:-${SYNCFUSION_API_KEY_PATH:-$HOME/.config/syncfusion/documentsdk.key}}"
fi

if [[ -z ${Syncfusion_API_Key_Path:-} ]]; then
	export Syncfusion_API_Key_Path="${SYNCFUSION_API_KEY_PATH:-$HOME/.config/syncfusion/documentsdk.key}"
fi

exec npx -y @syncfusion/blazor-assistant@latest "$@"
