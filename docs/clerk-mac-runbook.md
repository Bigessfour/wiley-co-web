# Clerk Mac runbook

Live path for Wiley Widget. Definition: `docs/production-completion-plan.md`.

Support contact: `steve.mckitrick@townofwiley.gov`.

## For the clerk

1. On the office Mac, open `http://127.0.0.1:5230`.
2. Town Hub is the first page. Wiley Widget opens the rate study. Apartments and TIKR open on the office NAS. Trash Truck opens its own site.
3. Rural Water says "Not available yet." That card has no site. Leave it.
4. Sign-in is off on this Mac. If a sign-in page appears, go back to Town Hub. Do not type a password into a page that says auth is disabled.
5. If the page does not load, or a panel has no numbers, contact `steve.mckitrick@townofwiley.gov`. Do not restart the NAS.

Monthly QuickBooks import: `docs/quickbooks-desktop-import-guide.md`.

## For the operator

Start, check, and stop:

```bash
./Scripts/start-clerk-local.sh
./Scripts/status-clerk-local.sh
./Scripts/stop-clerk-local.sh
```

Stop leaves Postgres running on `mr-storage`.

| Check                                  | What good looks like       |
| -------------------------------------- | -------------------------- |
| `./Scripts/status-clerk-local.sh`      | API and client running     |
| `http://127.0.0.1:5231/health`         | Healthy                    |
| Snapshot probe inside the start script | HTTP 200 for Water Utility |
| `mr-storage:5433`                      | Reachable over Tailscale   |

Logs: `/tmp/wiley-clerk-local/api.log`, or `WILEY_CLERK_STATE_DIR` if that variable is set.

There is no pager. The daily check is `./Scripts/status-clerk-local.sh`. If it fails, run `./Scripts/start-clerk-local.sh` once. If it still fails, read the API log before changing Postgres.

### When something breaks

| What failed                                  | What to do                                                                                                                             |
| -------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Browser cannot open `127.0.0.1:5230`         | `./Scripts/status-clerk-local.sh`, then start.                                                                                         |
| API log shows it cannot reach Postgres       | Check Tailscale to `mr-storage`. Do not create a second database.                                                                      |
| A new build is wrong                         | Check out the previous commit. `./Scripts/stop-clerk-local.sh`, then `./Scripts/start-clerk-local.sh`.                                 |
| A migration already ran and the app is wrong | Restore the `wileyco_dev` backup taken before that migrate. The backup job is not set up yet. Do not run the old Aurora reset runbook. |

### A new build

1. Merge to `main` through a pull request whose CI is green.
2. On this Mac, pull that commit.
3. Stop, then start, with the scripts above.
4. Write down the date, your name, the commit, and that `/health` and the snapshot probe succeeded.

### Backup

2026-11-14: `pg_dump -Fc` of `wileyco_dev` from `mr-storage:5433` finished in 12.5 seconds. The file is `~/wiley-backups/wileyco_dev-2026-11-14.dump` (148,070 bytes). It is not in git.

That dump is not yet a passed restore test. `wiley_app` cannot `CREATE DATABASE` on the NAS, so the dump was not restored there. A local Postgres restore stopped because this Mac does not have the `postgis` and `vector` extensions the dump creates.

Until a scheduled NAS backup exists, the recovery point is this one manual dump. Do not treat that as an ongoing backup. Next step is a Synology backup of the database directory, or a role that can create a scratch database on `mr-storage`, then one restore that can run `CREATE EXTENSION postgis` and `CREATE EXTENSION vector`. Do not restore over `wileyco_dev`.
