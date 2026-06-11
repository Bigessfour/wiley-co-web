#!/usr/bin/env bash
# Setup native PostgreSQL for Wiley Widget (no Docker).
# Targets clerk workstations: installs schema via EF migrations and optional Import Data seed.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_DIR="$ROOT_DIR/WileyCoWeb.Api"
LOCAL_SETTINGS="$API_DIR/appsettings.Development.local.json"
DB_NAME="${WILEY_DB_NAME:-wileyco_dev}"
DB_USER="${WILEY_DB_USER:-postgres}"
DB_PASSWORD="${WILEY_DB_PASSWORD:-password}"
DB_HOST="${WILEY_DB_HOST:-localhost}"
DB_PORT="${WILEY_DB_PORT:-5432}"

echo "=== Wiley Widget native PostgreSQL setup ==="
echo "Database: $DB_NAME on $DB_HOST:$DB_PORT (user: $DB_USER)"

if ! command -v psql >/dev/null 2>&1; then
	echo "ERROR: psql not found. Install PostgreSQL 16+ natively:" >&2
	echo "  macOS: brew install postgresql@16 && brew services start postgresql@16" >&2
	echo "  Windows: https://www.postgresql.org/download/windows/" >&2
	exit 1
fi

export PGPASSWORD="$DB_PASSWORD"

echo ">>> Ensuring database exists..."
if ! psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d postgres -tc "SELECT 1 FROM pg_database WHERE datname = '$DB_NAME'" | grep -q 1; then
	createdb -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" "$DB_NAME"
	echo "Created database $DB_NAME"
else
	echo "Database $DB_NAME already exists."
fi

CONN="Host=$DB_HOST;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"

echo ">>> Writing connection string to gitignored $LOCAL_SETTINGS"
mkdir -p "$(dirname "$LOCAL_SETTINGS")"
python3 - "$LOCAL_SETTINGS" "$CONN" <<'PY'
import json, sys
path, conn = sys.argv[1], sys.argv[2]
settings = {}
try:
    with open(path) as f:
        settings = json.load(f)
except FileNotFoundError:
    pass
settings.setdefault("ConnectionStrings", {})["DefaultConnection"] = conn
settings.setdefault("Database", {})
settings["Database"]["AllowDegradedStartup"] = False
settings["Database"]["SeedDevelopmentData"] = True
settings["Database"]["EnsureWorkspacePanelBudgetWhenEmpty"] = True
with open(path, "w") as f:
    json.dump(settings, f, indent=2)
    f.write("\n")
PY

echo ">>> Applying EF Core migrations..."
dotnet ef database update \
	--project "$ROOT_DIR/src/WileyWidget.Data/WileyWidget.Data.csproj" \
	--startup-project "$API_DIR/WileyCoWeb.Api.csproj" \
	--context AppDbContext

if [[ -d "$ROOT_DIR/Import Data" ]]; then
	echo ">>> Optional: seed reference data from Import Data (API must be running on 5231)"
	echo "    Run after starting API: ./Scripts/seed-local-import-data.sh"
else
	echo ">>> Import Data folder not found; skip seed or copy council exports into repo/Import Data"
fi

echo ""
echo "Native PostgreSQL setup complete."
echo "Start stack: ./start-local.sh (skip Docker) or dotnet run API + client manually."
