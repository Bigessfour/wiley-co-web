#!/bin/bash
set -euo pipefail

echo "=== Stopping Wiley Widget local full stack ==="

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOGS_DIR="$ROOT_DIR/TestResults/local-run"
API_PID_FILE="$LOGS_DIR/api.pid"
CLIENT_PID_FILE="$LOGS_DIR/client.pid"

# Kill by PID files if present (safer, avoids self-match)
if [ -f "$API_PID_FILE" ]; then
	API_PID=$(cat "$API_PID_FILE" 2>/dev/null || true)
	if [ -n "$API_PID" ] && kill -0 "$API_PID" 2>/dev/null; then
		echo "Stopping API (PID $API_PID)..."
		kill -TERM "$API_PID" 2>/dev/null || true
		sleep 1
		kill -9 "$API_PID" 2>/dev/null || true
	fi
	rm -f "$API_PID_FILE"
fi

if [ -f "$CLIENT_PID_FILE" ]; then
	CLIENT_PID=$(cat "$CLIENT_PID_FILE" 2>/dev/null || true)
	if [ -n "$CLIENT_PID" ] && kill -0 "$CLIENT_PID" 2>/dev/null; then
		echo "Stopping Client (PID $CLIENT_PID)..."
		kill -TERM "$CLIENT_PID" 2>/dev/null || true
		sleep 1
		kill -9 "$CLIENT_PID" 2>/dev/null || true
	fi
	rm -f "$CLIENT_PID_FILE"
fi

# Fallback: kill by port (macOS friendly, no -f self-match issue)
echo "Cleaning ports 5230/5231..."
for port in 5230 5231; do
	pids=$(lsof -ti :$port 2>/dev/null || true)
	if [ -n "$pids" ]; then
		echo "  Killing processes on :$port: $pids"
		echo "$pids" | xargs kill -9 2>/dev/null || true
	fi
done

# Extra: aggressively kill any lingering Blazor dev server / watch children.
# Incomplete cleanup is the #1 cause of 0-5% WASM boot hangs (zombie :5230, exit 134).
pkill -9 -f 'blazor-devserver' 2>/dev/null || true
pkill -9 -f 'dotnet-watch\.dll run.*WileyCoWeb' 2>/dev/null || true
pkill -9 -f 'dotnet watch run.*WileyCoWeb' 2>/dev/null || true
pkill -9 -f 'dotnet run.*WileyCoWeb\.csproj' 2>/dev/null || true
pkill -9 -f 'Microsoft.AspNetCore.Components.WebAssembly.DevServer' 2>/dev/null || true
sleep 1

# Verify ports 5230/5231 are free; retry once if a zombie listener survives.
for port in 5230 5231; do
	pids=$(lsof -ti :$port 2>/dev/null || true)
	if [ -n "$pids" ]; then
		echo "  Port :$port still in use (PIDs: $pids); retrying kill after 2s..."
		sleep 2
		echo "$pids" | xargs kill -9 2>/dev/null || true
		pkill -9 -f 'Microsoft.AspNetCore.Components.WebAssembly.DevServer' 2>/dev/null || true
		sleep 1
		pids=$(lsof -ti :$port 2>/dev/null || true)
		if [ -n "$pids" ]; then
			echo "  WARNING: Port :$port still held by: $pids (browser boot may hang at 0-5%)" >&2
		fi
	fi
done

# Optional: leave DB running (docker compose keeps it lightweight)
echo ""
echo "DB (if started via docker-compose.dev.yml) left running."
echo "To stop DB too: docker compose -f docker-compose.dev.yml down"
echo "Stack stopped."
