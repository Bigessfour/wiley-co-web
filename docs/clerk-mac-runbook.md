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

A new backup of Deb's PC can send new ledger spreadsheets through that same import. The company file is not read. See "Deb's backup" below.

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

### Backup proof

Dump taken 2026-11-14. Custom format (`pg_dump -Fc`, gzip). 148070 bytes. Path `~/wiley-backups/wileyco_dev-2026-11-14.dump`. The file is not in git. The archive header says it was created 2026-11-14 09:14:34 MST from PostgreSQL 17.11 on `wileyco_dev`, by pg_dump 18.7 (Homebrew). The dump finished in 12.5 seconds.

Closest reproducible command. The password stays in the operator environment (the Development local connection string). Do not write it in this file.

```bash
pg_dump -Fc -h mr-storage -p 5433 -U wiley_app -d wileyco_dev -f ~/wiley-backups/wileyco_dev-2026-11-14.dump
```

`wiley_app` lacks `CREATEDB`, so it cannot create a scratch database on the NAS. A restore on this Mac currently fails because local Postgres does not have `postgis` and `vector`. The dump also creates `cube`, `earthdistance`, `pg_trgm`, `unaccent`, and `uuid-ossp`. Do not restore over `wileyco_dev`.

RPO: not yet measured — first restore still pending. Until a scheduled NAS backup exists, the only recovery point is this one manual dump from 2026-11-14.

RTO: not yet measured — first restore still pending. The 12.5 second dump time is not a restore time.

#### Restore procedure

This cannot run today. Run it once a superuser can create a scratch database on a Postgres that can create `postgis` and `vector`.

1. Create a scratch database. Only a superuser, or a role with `CREATEDB`, can do this. `wiley_app` cannot. Use a new name. Do not use `wileyco_dev`.

```sql
CREATE DATABASE wileyco_dev_restore_drill;
```

2. Restore the dump into that database. Do not pass `--create`. The dump contains `CREATE DATABASE wileyco_dev`, and `--create` would target the live name.

```bash
pg_restore --no-owner --no-acl -h mr-storage -p 5433 -U <superuser> -d wileyco_dev_restore_drill ~/wiley-backups/wileyco_dev-2026-11-14.dump
```

If this host still cannot `CREATE EXTENSION postgis` or `CREATE EXTENSION vector`, stop and install those extensions. Do not point the app at the scratch database.

3. Confirm a known table matches the source. On both `wileyco_dev` and `wileyco_dev_restore_drill`:

```sql
SELECT count(*) FROM ledger_entries;
```

The two counts must match. Record both numbers and the date.

4. Drop the scratch database.

```sql
DROP DATABASE wileyco_dev_restore_drill;
```

Next step: a role that can create a scratch database, on a server with `postgis` and `vector`, then this procedure once. Record the date in this section. A scheduled Synology backup of the database is still required. This one file is not an ongoing backup.

### Check listings and Deb's backup

The files that change are the meeting workbooks under `CHECK LISTING/2026 CHECK LISTING` on `mr-storage`. Those are check listings (check number, date, payee, amount, and what it was for), not the QuickBooks company file. The company files on the share were last saved in March 2025, and Deb's PC backup does not contain one.

`./Scripts/import-deb-backup.sh` copies those workbooks, turns each check into the transaction list the QuickBooks import already accepts, and reports what would load. General Fund and bank balances are left out. A check that is already in the ledger is skipped, and the new checks in that same workbook can still load.

```bash
./Scripts/import-deb-backup.sh
./Scripts/import-deb-backup.sh --commit
```

The first command writes nothing. `--commit` writes into `wileyco_dev`. The manual import panel is unchanged. The start script does not run this. `--install` checks every 15 minutes and still only reports until someone runs `--commit`.
