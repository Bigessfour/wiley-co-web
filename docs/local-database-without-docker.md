# Local database without Docker (clerk workstation)

Wiley Widget uses **PostgreSQL** (same dialect as Aurora). Docker is optional; clerks can run a **native PostgreSQL** install with scripts and migrations kept in this repo.

## What lives in the repo

| Asset                               | Purpose                                                                          |
| ----------------------------------- | -------------------------------------------------------------------------------- |
| `src/WileyWidget.Data/Migrations/`  | Canonical EF Core schema (apply with `dotnet ef database update`)                |
| `Scripts/setup-native-postgres.sh`  | Create `wileyco_dev`, write `appsettings.Development.local.json`, run migrations |
| `Scripts/seed-local-import-data.sh` | POST council QuickBooks / reference files from `Import Data/`                    |
| `Import Data/`                      | Council export folder (not committed if large; copy from town share)             |
| `docker-compose.dev.yml`            | Optional Docker Postgres for developers who use Docker                           |

## Clerk setup (macOS example)

```bash
brew install postgresql@16
brew services start postgresql@16
./Scripts/setup-native-postgres.sh
./start-local.sh   # or run API + client manually without Docker
```

Connection string written to gitignored `WileyCoWeb.Api/appsettings.Development.local.json`:

`Host=localhost;Database=wileyco_dev;Username=postgres;Password=password`

Adjust `WILEY_DB_USER`, `WILEY_DB_PASSWORD`, `WILEY_DB_HOST`, `WILEY_DB_PORT` when invoking the setup script.

## After schema apply

1. Start API: `dotnet run --project WileyCoWeb.Api/WileyCoWeb.Api.csproj`
2. Seed reference data (if `Import Data/` is present): `./Scripts/seed-local-import-data.sh`
3. Start client: `dotnet run --project WileyCoWeb.csproj`

`Database:AllowDegradedStartup` must be **false** in local settings so the API uses real Postgres (not in-memory fallbacks).

## Windows

Install PostgreSQL from https://www.postgresql.org/download/windows/, ensure `psql` is on PATH, then run `Scripts/setup-native-postgres.sh` from Git Bash or WSL.
