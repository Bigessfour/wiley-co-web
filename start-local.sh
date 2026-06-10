#!/bin/bash
set -euo pipefail

# start-local.sh - Proper single-command way to start the full Wiley Widget stack locally
# Client: http://localhost:5230
# API:    http://localhost:5231 (with real Postgres via docker-compose.dev.yml)
#
# Prerequisites (one-time or as needed):
# - Docker running (for DB)
# - .NET 9 SDK
# - Syncfusion keys configured (launchctl / local json / keychain as per README)
# - xAI key will be prompted in the UI after start (via Jarvis panel)
#
# Usage:
#   ./start-local.sh
#   (then Ctrl+C to stop; or use the kill task/script)
#
# It will:
# 1. Start Postgres via docker-compose.dev.yml (if not running)
# 2. Start API in background (logs to console + file)
# 3. Start Client in background
# 4. Wait for both to be ready
# 5. Open browser to client (if possible)
# 6. Print instructions (use the in-app prompt for xAI key)
#
# To stop cleanly: use Scripts/kill-dotnet.ps1 or pkill -f "dotnet run"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJECT="$ROOT_DIR/WileyCoWeb.Api/WileyCoWeb.Api.csproj"
CLIENT_PROJECT="$ROOT_DIR/WileyCoWeb.csproj"
LOGS_DIR="$ROOT_DIR/TestResults/local-run"
API_LOG="$LOGS_DIR/api.log"
CLIENT_LOG="$LOGS_DIR/client.log"
API_PID_FILE="$LOGS_DIR/api.pid"
CLIENT_PID_FILE="$LOGS_DIR/client.pid"

mkdir -p "$LOGS_DIR"

# Small addition: look for and kill any running instances from previous starts
# This frees ports 5230/5231 and avoids "address already in use" errors.
# Reuses the robust logic from stop-local.sh (PID files + port cleanup).
echo ">>> Stopping any previous running instances of the stack (if any)..."
./stop-local.sh || true
echo ""

# Clean test / playwright artifact noise early. These files trigger dotnet watch "file changed" events
# during the critical first WASM boot window, causing extra MSBuild churn while the browser is trying
# to fetch ~100 MB of runtime + app assets. This has been observed to contribute to "Loading dotnet.js…"
# hangs in real Chromium (puppeteer / playwright / user tabs) even when node validate passes.
echo ">>> Cleaning test-results / playwright artifacts (reduces dotnet watch noise during boot)..."
rm -rf TestResults/playwright* test-results/ .playwright-artifacts* 2>/dev/null || true
mkdir -p "$LOGS_DIR"

echo "=== Wiley Widget Local Full-Stack Start ==="
echo "Root: $ROOT_DIR"
echo "DB: docker-compose.dev.yml (postgres:16-alpine)"
echo "API: $API_PROJECT -> http://localhost:5231"
echo "Client: $CLIENT_PROJECT -> http://localhost:5230"
echo ""

# 1. Ensure DB (real Postgres via Docker for non-degraded mode)
echo ">>> Starting/ensuring Postgres (real DB, no degraded mode)..."

# Kill any running instances of the DB container or processes on 5432 from previous runs
echo ">>> Cleaning up any previous Postgres instances (containers or native processes on 5432)..."
docker compose -f "$ROOT_DIR/docker-compose.dev.yml" down --remove-orphans 2>/dev/null || true
# Kill any process listening on 5432 (native postgres or stuck proxy)
lsof -ti:5432 | xargs kill -9 2>/dev/null || true
# Double-check and remove any lingering container by name
docker ps -aq --filter "name=wileyco-dev-postgres" | xargs -r docker rm -f 2>/dev/null || true

# Check for port conflict early (common cause of bind failures)
if lsof -i :5432 -sTCP:LISTEN >/dev/null 2>&1; then
	echo "WARNING: Port 5432 is still in use on the host after cleanup."
	echo "Running processes on :5432:"
	lsof -i :5432 -sTCP:LISTEN
	echo ""
	echo "Suggestions to free the port:"
	echo "  - If native Postgres: brew services stop postgresql@16  (or equivalent)"
	echo "  - If Docker container: docker ps | grep postgres ; docker stop <id>"
	echo "  - Or kill by PID from lsof output above: kill -9 <PID>"
	echo ""
	echo "The compose up below may fail. If it does, you can:"
	echo "  - Free the port and re-run ./start-local.sh"
	echo "  - Or switch to native Postgres (update connection in .local.json and start manually)"
	echo "  - Or map a different host port in docker-compose.dev.yml (e.g. 5433:5432) and adjust .local.json"
	echo ""
fi

if ! docker compose -f "$ROOT_DIR/docker-compose.dev.yml" ps --services --filter "status=running" | grep -q postgres; then
	docker compose -f "$ROOT_DIR/docker-compose.dev.yml" up -d
	echo "Waiting for Postgres to be healthy..."
	for i in {1..30}; do
		if docker compose -f "$ROOT_DIR/docker-compose.dev.yml" exec -T postgres pg_isready -U postgres -d wileyco_dev >/dev/null 2>&1; then
			echo "Postgres ready."
			break
		fi
		sleep 1
		if [ $i -eq 30 ]; then
			echo "Postgres not ready after 30s. Check docker logs."
			echo "You can still continue with API+Client (they may fall back to degraded mode if DB conn fails)."
			# Do not exit; let the rest of the script run so user can at least get the UI up
		fi
	done
else
	echo "Postgres already running."
fi

# Optional: ensure local settings disable degraded (user can edit)
LOCAL_SETTINGS="$ROOT_DIR/WileyCoWeb.Api/appsettings.Development.local.json"
if [ ! -f "$LOCAL_SETTINGS" ]; then
	echo ">>> Creating default $LOCAL_SETTINGS (real DB + no synthetic fallbacks). Edit as needed."
	cat >"$LOCAL_SETTINGS" <<'JSON_EOF'
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=wileyco_dev;Username=postgres;Password=password"
  },
  "Database": {
    "AllowDegradedStartup": false,
    "SeedDevelopmentData": true,
    "EnsureWorkspacePanelBudgetWhenEmpty": true
  },
  "WorkspacePanels": {
    "Fallback": {
      "UseSyntheticCapitalGapWhenNoBudgetData": false,
      "UseSyntheticDebtCoverageWhenEnterpriseMissing": false
    }
  }
}
JSON_EOF
fi

# 2. Start API
echo ">>> Starting API (background, logs -> $API_LOG)..."
: >"$API_LOG"
# Kill any previous on port
lsof -ti:5231 | xargs kill -9 2>/dev/null || true
(
	cd "$ROOT_DIR"
	ASPNETCORE_ENVIRONMENT=Development \
		DOTNET_ENVIRONMENT=Development \
		WILEY_WORKSPACE_API_BASE_ADDRESS=http://localhost:5231 \
		dotnet run --project "$API_PROJECT" --no-launch-profile --urls http://localhost:5231 \
		>"$API_LOG" 2>&1 &
	echo $! >"$API_PID_FILE"
)

# 3. Start Client
# First do a full synchronous build. This ensures the entire _framework/ tree (dotnet.*, blazor.*,
# all fingerprinted .wasm, jsModule*, wasmNative, icu, maps, gz, etc.) is materialized on disk
# *before* the dev server starts accepting browser requests. We use `dotnet run` (not watch) for
# stability — dotnet watch restart races leave zombie listeners on :5230 and browsers hang at 0-5%.
echo ">>> Pre-building client to fully materialize _framework assets (prevents partial-manifest hangs for browsers)..."
dotnet build "$CLIENT_PROJECT" --no-restore --nologo -v q || echo " (build had warnings; continuing)"

echo ">>> Starting Client (background, stable dotnet run, logs -> $CLIENT_LOG)..."
: >"$CLIENT_LOG"
lsof -ti:5230 | xargs kill -9 2>/dev/null || true
(
	cd "$ROOT_DIR"
	ASPNETCORE_ENVIRONMENT=Development \
		DOTNET_ENVIRONMENT=Development \
		WILEY_WORKSPACE_API_BASE_ADDRESS=http://localhost:5231 \
		dotnet run --project "$CLIENT_PROJECT" --no-launch-profile --no-build --urls http://localhost:5230 \
		>"$CLIENT_LOG" 2>&1 &
	echo $! >"$CLIENT_PID_FILE"
)

API_PID=$(cat "$API_PID_FILE")
CLIENT_PID=$(cat "$CLIENT_PID_FILE")
echo "API PID: $API_PID | Client PID: $CLIENT_PID"

# 4. Wait for ready
echo ">>> Waiting for services (up to 120s)..."
for i in {1..120}; do
	API_READY=0
	CLIENT_READY=0
	if curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5231/health | grep -q "200"; then API_READY=1; fi
	if curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5230 | grep -q "200"; then CLIENT_READY=1; fi
	if [ $API_READY -eq 1 ] && [ $CLIENT_READY -eq 1 ]; then
		echo "Both services ready!"
		if node "$ROOT_DIR/Scripts/validate-blazor-boot-resources.mjs" --base http://127.0.0.1:5230; then
			echo "Boot resource manifest validated."
		else
			echo "WARNING: Boot resources failed validation — client may be stale. Re-run ./stop-local.sh && ./start-local.sh"
		fi

		# Pre-warm the critical early runtime assets that real browsers (not just node fetch) request first.
		# The browser-driven boot (puppeteer, playwright Chromium, user Chrome tabs) often hangs at
		# "Loading dotnet.js…" or 0-2% even when the node validator + plain curls say 200, because the
		# dev server may still be materializing the full set (including gz, maps, native, jsModuleRuntime
		# entries) when the first browser navigation arrives. Doing real GETs here gives the dev server
		# a chance to complete publishing before we open the clean Chrome or tell the user the UI is ready.
		echo ">>> Pre-warming critical browser runtime assets (dotnet.js, blazor.webassembly.js, main WASM, jsModule entries)..."
		PREWARM_OK=1
		for attempt in 1 2 3 4 5; do
			PREWARM_OK=1
			for asset in \
				"_framework/dotnet.js" \
				"_framework/blazor.webassembly.js" \
				"_framework/blazor.boot.json"; do
				if ! curl -s -f -o /dev/null "http://127.0.0.1:5230/$asset"; then
					PREWARM_OK=0
					break
				fi
			done
			# Also warm the current main WASM (from the live boot.json) and one runtime module if possible
			MAIN_WASM=$(curl -s http://127.0.0.1:5230/_framework/blazor.boot.json 2>/dev/null | node -e '
        try {
          const b = JSON.parse(require("fs").readFileSync(0,"utf8"));
          const fp = b.resources && b.resources.fingerprinting || {};
          const main = b.mainAssemblyName || "WileyCoWeb";
          for (const k of Object.keys(fp)) {
            if (k.startsWith(main + ".") && k.endsWith(".wasm")) { console.log(k); break; }
          }
        } catch(e) {}
      ' 2>/dev/null || true)
			if [ -n "$MAIN_WASM" ]; then
				curl -s -f -o /dev/null "http://127.0.0.1:5230/_framework/$MAIN_WASM" || PREWARM_OK=0
			fi
			# Try to warm a jsModuleRuntime entry (the real browser loader needs these early)
			RUNTIME_JS=$(curl -s http://127.0.0.1:5230/_framework/blazor.boot.json 2>/dev/null | node -e '
        try {
          const b = JSON.parse(require("fs").readFileSync(0,"utf8"));
          const r = (b.resources && b.resources.jsModuleRuntime) || {};
          const k = Object.keys(r)[0];
          if (k) console.log(k);
        } catch(e) {}
      ' 2>/dev/null || true)
			if [ -n "$RUNTIME_JS" ]; then
				curl -s -f -o /dev/null "http://127.0.0.1:5230/_framework/$RUNTIME_JS" || PREWARM_OK=0
			fi

			if [ $PREWARM_OK -eq 1 ]; then
				echo "Critical runtime assets pre-warmed successfully (attempt $attempt)."
				break
			fi
			echo "  Pre-warm attempt $attempt not yet complete, waiting 3s..."
			sleep 3
		done
		if [ $PREWARM_OK -ne 1 ]; then
			echo "WARNING: Pre-warm did not fully succeed after retries. Browser boot may still be slow on first load."
			echo '         If you see a long hang at "Loading dotnet.js…", close the tab and re-run this script.'
		fi

		break
	fi
	if [ $i -eq 120 ]; then
		echo "Timeout. Check logs:"
		echo "  tail -f $API_LOG"
		echo "  tail -f $CLIENT_LOG"
		exit 1
	fi
	sleep 1
	if [ $((i % 10)) -eq 0 ]; then echo "Still waiting... ($i/120)"; fi
done

# 4b. Verify client supervisor is still healthy (catches dotnet watch exit-134 / zombie :5230)
if [ -x "$ROOT_DIR/Scripts/assert-client-process-healthy.sh" ]; then
	echo ">>> Verifying client process health..."
	"$ROOT_DIR/Scripts/assert-client-process-healthy.sh" "$CLIENT_PID" "$CLIENT_LOG"
fi

# Re-validate boot manifest immediately before seed/chrome (catches drift after late rebuilds)
if node "$ROOT_DIR/Scripts/validate-blazor-boot-resources.mjs" --base http://127.0.0.1:5230; then
	echo "Boot resource manifest re-validated before browser launch."
else
	echo "WARNING: Boot resources failed re-validation — run ./stop-local.sh && ./start-local.sh"
fi

# 5. Seed the QuickBooks data from the "Import Data" folder into the Postgres DB.
# This uses the existing /api/workspace/reference-data/import endpoint and the
# WorkspaceReferenceDataImportService (the same code path as the clerk UI import
# and the production sample data load).
# It will:
# - Create import_batches and source_files (with file_hash for the dedup guard in the import service).
# - Import ledger_entries from the transaction-list and general-ledger files (the main data for workspace calculations).
# - Seed reference data (customers, vendors, chart of accounts, etc.) from the other files.
# - The "wsd" and "util" variants will be scoped via entry_scope / variant.
# The import is idempotent (skips files whose hash is already in source_files).
# After this, the data will be visible in the UI panels, calculations, and Jarvis knowledge as "imported" sample data.
echo ">>> Seeding QuickBooks sample data from 'Import Data/' folder (ledger + reference data)..."
curl -s -X POST "http://127.0.0.1:5231/api/workspace/reference-data/import" \
	-H "Content-Type: application/json" \
	-d '{
    "ExplicitImportDataPath": "Import Data",
    "IncludeSampleLedgerData": true,
    "ApplyDefaultEnterpriseBaselines": true
  }' | cat || echo " (seed request sent; check API logs or the import history in the UI if the data is not immediately visible. The service may require the path setting in .local.json or dev auth context.)"

# 6. Launch a *clean* Chrome (dedicated profile, no stale WASM manifest cache from previous runs/tabs).
# Plain `open` re-uses an existing tab for the origin, which is the #1 cause of "stuck at 2% / Loading dotnet.js" after a restart
# (the tab has an in-memory or disk-cached blazor.boot.json pointing at old fingerprinted hashes that no longer exist).
# The debug launcher gives you a fresh profile + remote debugging port (9222) so you can also attach chrome-devtools-mcp or run
# the ui-audit / fresh-tab-boot-test / diagnose scripts.
URL="http://127.0.0.1:5230/wiley-workspace"
if [ -x "./Scripts/start-chrome-debug.sh" ]; then
	echo ">>> Launching clean Chrome profile for reliable cold boot (avoids stale tab cache)..."
	./Scripts/start-chrome-debug.sh "$URL" || echo " (chrome debug launcher returned non-zero; you can run it manually)"
else
	echo ">>> Falling back to 'open' (WARNING: may reuse a stale tab — close any old 5230 tabs first!)"
	if command -v open >/dev/null; then
		open "$URL"
	fi
fi

echo ""
echo "=== Stack running ==="
echo "UI (use this exact URL in a *new* tab or the launched clean Chrome): http://127.0.0.1:5230/wiley-workspace"
echo "API: http://127.0.0.1:5231 (health: /health , /api/ai/health)"
echo ""
echo "CRITICAL FOR BOOT (stuck at 2% / Loading dotnet.js / 0% is almost always a stale browser tab):"
echo "  1. CLOSE any previously-opened tabs pointing at localhost:5230 or 127.0.0.1:5230 *completely* (Cmd/Ctrl+W on the tab, or quit that Chrome window)."
echo "  2. Prefer the clean profile launched above, or manually run: ./Scripts/start-chrome-debug.sh http://127.0.0.1:5230/wiley-workspace"
echo "  3. Hard refresh (Ctrl+Shift+R or Cmd+Shift+R) is often NOT enough; the clean --user-data-dir profile is the reliable path."
echo "  4. If it still hangs low, run: node Scripts/validate-blazor-boot-resources.mjs --base http://127.0.0.1:5230"
echo "     (the enhanced validator now checks runtime js + wasm support files, not just app fingerprinting)."
echo ""
echo "IMPORTANT (per previous plan):"
echo " - Use the in-app prompt in the Decision Support / Jarvis panel to provide your xAI key (secure, local-only, writes to gitignored .local.json)."
echo " - Syncfusion keys should already be configured via launchctl / keychain / local files."
echo " - Real Postgres is used (no degraded mode if .local.json is in place)."
echo " - The QuickBooks data from 'Import Data/' has been seeded via the reference data import endpoint (import_batches, source_files with hashes for dedup, ledger_entries for the workspace math, and reference data like customers/vendors/COA)."
echo ""
echo "To stop: ./stop-local.sh or run the VS Code task '🛑 Kill Local Debug Web Stack' or pkill -f 'dotnet run'"
echo "Logs: tail -f $API_LOG   |   tail -f $CLIENT_LOG"
echo ""
echo "Tip: For hot reload during dev, run manually in a separate terminal:"
echo "  dotnet watch run --project WileyCoWeb.csproj --no-launch-profile --urls http://localhost:5230"
echo "For full Syncfusion component coverage in Chrome + chrome-devtools-mcp: after clean boot, you can point chrome-devtools-mcp at http://127.0.0.1:9222 or run the ui-audit script / playwright workspace-syncfusion-controls.spec.ts (with CI=true to reuse this stack)."

# Optional: tail logs in foreground? For now, exit so user can background or use another term.
# To keep simple, we can background the whole or let user control.
