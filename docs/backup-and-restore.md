# Backup and restore

Two data planes. Aurora holds the Wiley Widget workspace. The NAS holds ClerkSuite apartments (SQLite) and the document vault.

Do not restore either plane during a council meeting. Stop writes first. Do not copy the live SQLite file over SMB while the container is running.

## What was tested here

`Scripts/backup-restore-drill.sh` builds a throwaway SQLite file, copies it, deletes the row, copies the backup back, and checks the row. That is the file-level procedure. It does not touch Aurora or the NAS.

Aurora point-in-time restore and a DSM restore onto the live volume were not run. Those need the `steve` profile (session was expired) and a maintenance window.

## Aurora (Wiley Widget)

| Item | Value |
| --- | --- |
| Account | `570912405222` (profile `steve`) |
| Region | `us-east-2` |
| Cluster | `wiley-co-aurora-db-encrypted` |

Backup is Aurora automated backups and snapshots. Confirm a recent snapshot before a destructive change:

```bash
aws rds describe-db-cluster-snapshots \
  --profile steve --region us-east-2 \
  --db-cluster-identifier wiley-co-aurora-db-encrypted \
  --query 'DBClusterSnapshots[].{Id:DBClusterSnapshotIdentifier,Time:SnapshotCreateTime,Status:Status}'
```

Restore drill (maintenance window, new cluster, not in place):

1. Restore to a new cluster with `restore-db-cluster-to-point-in-time` and a new identifier such as `wiley-co-aurora-db-restore-drill`.
2. Point a throwaway API config at that cluster. Do not change production App Runner.
3. Call `/health` and one snapshot read.
4. Delete the drill cluster when the check is recorded.

In-place restore replaces production. Do that only when the production cluster is the thing you are recovering, and record the snapshot id you restored from.

## NAS (apartments database and documents)

Host `mr-storage` (DS225+). The committed ClerkSuite compose uses SQLite. Step-by-step DSM restore is in the Wiley Apartments repo at `deploy/synology/BACKUP-RESTORE.md`.

| Plane | Path |
| --- | --- |
| SQLite | Docker volume `clerksuite_clerksuite-data` → `/volume1/@docker/volumes/clerksuite_clerksuite-data/_data/clerksuite.db` |
| Documents | `/volume1/apartments/docs` |

The apartments working tree has an uncommitted compose that points the container at Postgres on the NAS. Before a restore, confirm what is actually running:

```bash
ssh mr-storage 'sudo /usr/local/bin/docker exec clerksuite printenv ClerkSuite__DatabaseProvider'
```

If that prints `Sqlite`, follow the SQLite volume steps below. If it prints `Postgres`, dump that database with the NAS Postgres tools and do not restore the SQLite file over it.

DSM Active Backup (or Hyper Backup / snapshots) must include the document share and the Docker volume. Confirm the last successful job in DSM before relying on it.

Live SQLite restore (maintenance window):

1. `ssh mr-storage` and stop ClerkSuite: `cd /volume1/docker/clerksuite && sudo /usr/local/bin/docker compose stop`.
2. Restore `/volume1/apartments/docs` from the DSM backup to that same path.
3. Restore `clerksuite.db` (and `-wal` / `-shm` if the backup has them) into the Docker volume. Do not copy that file over SMB while the container is running.
4. Start the stack and open `http://mr-storage:8082` from the office network.
5. Sign in and open one known tenant.

## After either restore

Record the date, who ran it, the snapshot or backup job name, and the check you opened (API `/health`, or one apartment tenant).
