#!/usr/bin/env bash
# Launch Google Chrome with remote debugging for chrome-devtools-mcp / UI audits.
# Does not use Cursor's embedded browser.
set -euo pipefail

PORT="${CHROME_DEBUG_PORT:-9222}"
PROFILE="${CHROME_USER_DATA_DIR:-/tmp/wiley-chrome-profile-stable}"
URL="${1:-http://127.0.0.1:5230/wiley-workspace}"

CHROME_MAC="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
CHROME_LINUX="/usr/bin/google-chrome"

if [[ -x $CHROME_MAC ]]; then
	CHROME="$CHROME_MAC"
elif command -v google-chrome >/dev/null 2>&1; then
	CHROME="$(command -v google-chrome)"
elif [[ -x $CHROME_LINUX ]]; then
	CHROME="$CHROME_LINUX"
else
	echo "Google Chrome not found." >&2
	exit 1
fi

has_workspace_tab() {
	local tabs
	tabs=$(curl -s "http://127.0.0.1:$PORT/json/list" 2>/dev/null || true)
	echo "$tabs" | grep -q 'wiley-workspace' 2>/dev/null
}

open_workspace_tab() {
	# Chrome DevTools HTTP API: open a new tab at URL
	local encoded
	encoded=$(python3 -c "import urllib.parse, sys; print(urllib.parse.quote(sys.argv[1], safe=''))" "$URL" 2>/dev/null || echo "$URL")
	if curl -s -f -o /dev/null "http://127.0.0.1:$PORT/json/new?$encoded" 2>/dev/null; then
		echo "Opened workspace tab via CDP: $URL"
		return 0
	fi
	if command -v open >/dev/null 2>&1 && [[ -x $CHROME_MAC ]]; then
		open -a "Google Chrome" --args --new-tab "$URL" 2>/dev/null || true
		echo "Opened workspace tab via macOS open: $URL"
		return 0
	fi
	"$CHROME" --new-tab "$URL" &
	echo "Opened workspace tab in new Chrome instance: $URL"
}

if lsof -i ":$PORT" -sTCP:LISTEN >/dev/null 2>&1; then
	echo "Chrome debug port $PORT already listening."
	if has_workspace_tab; then
		echo "Workspace tab already open in debug Chrome."
	else
		echo "No wiley-workspace tab found — opening $URL"
		open_workspace_tab
	fi
else
	echo "Starting Chrome on debug port $PORT (profile: $PROFILE)"
	"$CHROME" --remote-debugging-port="$PORT" --user-data-dir="$PROFILE" "$URL" &
	sleep 2
fi

echo "Remote debugging: http://127.0.0.1:$PORT"
echo "Configure chrome-devtools-mcp with: --browser-url=http://127.0.0.1:$PORT"
