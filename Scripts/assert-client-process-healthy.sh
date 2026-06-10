#!/usr/bin/env bash
# Verifies the Blazor WASM dev client is healthy before declaring start-local.sh success.
# A crashed dotnet watch / zombie listener on :5230 is the #1 cause of 0-5% boot hangs.
set -euo pipefail

CLIENT_PID="${1:?Usage: assert-client-process-healthy.sh CLIENT_PID CLIENT_LOG}"
CLIENT_LOG="${2:?Usage: assert-client-process-healthy.sh CLIENT_PID CLIENT_LOG}"

fail() {
	echo "FAIL: $1" >&2
	exit 1
}

if ! kill -0 "$CLIENT_PID" 2>/dev/null; then
	fail "Client supervisor PID $CLIENT_PID is not running. Run ./stop-local.sh && ./start-local.sh"
fi

if [ -f "$CLIENT_LOG" ]; then
	SESSION_LOG=$(strings "$CLIENT_LOG" 2>/dev/null | tail -120 || true)
	if ! echo "$SESSION_LOG" | grep -q 'Application started'; then
		fail "client.log has no 'Application started' yet (see $CLIENT_LOG). Run ./stop-local.sh && ./start-local.sh"
	fi
	# Only fail on crash markers in the tail after a successful start signal for this session
	if echo "$SESSION_LOG" | grep -qE 'Exited with error code|Failed to bind to address|Hosting failed to start'; then
		fail "client.log shows a crash after startup (see tail $CLIENT_LOG). Run ./stop-local.sh && ./start-local.sh"
	fi
fi

LISTENERS=$(lsof -ti :5230 2>/dev/null || true)
if [ -z "$LISTENERS" ]; then
	fail "Nothing is listening on port 5230."
fi

LISTENER_COUNT=$(echo "$LISTENERS" | wc -l | tr -d ' ')
if [ "$LISTENER_COUNT" -gt 4 ]; then
	fail "Too many processes on port 5230 ($LISTENERS). Run ./stop-local.sh to clear zombie listeners."
fi

echo "OK client process healthy (PID $CLIENT_PID, :5230 listener present)."
