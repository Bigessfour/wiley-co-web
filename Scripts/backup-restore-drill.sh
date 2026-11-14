#!/usr/bin/env bash
# Throwaway SQLite backup and restore. Does not touch Aurora or the NAS.
set -euo pipefail

root="$(mktemp -d)"
trap 'rm -rf "$root"' EXIT
export DRILL_ROOT="$root"

python3 - <<'PY'
import os
import shutil
import sqlite3
import sys

root = os.environ["DRILL_ROOT"]
live = os.path.join(root, "clerksuite.db")
backup = os.path.join(root, "clerksuite.db.bak")

conn = sqlite3.connect(live)
conn.execute("create table drill (id integer primary key, note text not null)")
conn.execute("insert into drill (note) values ('restore-me')")
conn.commit()
conn.close()

shutil.copy2(live, backup)

conn = sqlite3.connect(live)
conn.execute("delete from drill")
conn.commit()
conn.close()

shutil.copy2(backup, live)

conn = sqlite3.connect(live)
row = conn.execute("select note from drill").fetchone()
conn.close()
if row != ("restore-me",):
    sys.exit("restore check failed")
print("backup-restore drill ok")
PY
