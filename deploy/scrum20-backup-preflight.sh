#!/bin/sh
# AI-assisted: run manually on the test server; never migrates or restarts services.
set -eu
umask 077
cd "$(dirname "$0")/.."
test -f deploy/.env || { echo 'Missing server deploy/.env; refusing to continue.' >&2; exit 1; }
compose() { docker compose --env-file deploy/.env -f deploy/docker-compose.yml "$@"; }
backup_root="$HOME/trustcabinet-backups"
mkdir -p "$backup_root"
backup_dir=$(mktemp -d "$backup_root/scrum20-$(date -u +%Y%m%dT%H%M%SZ)-XXXXXX")
echo "Backup directory: $backup_dir"
git rev-parse HEAD > "$backup_dir/server-commit.txt"
compose images -q app > "$backup_dir/app-image.txt"
compose exec -T postgres psql -X -U trustcabinet -d trustcabinet -v ON_ERROR_STOP=1 \
    -c 'SELECT migration_id FROM __ef_migrations_history ORDER BY migration_id;' > "$backup_dir/migrations.txt"
# Do not overwrite a previous backup; only rename a completely written archive.
compose exec -T postgres pg_dump -U trustcabinet -d trustcabinet \
    --format=custom --no-owner --no-privileges > "$backup_dir/database.dump.partial"
mv "$backup_dir/database.dump.partial" "$backup_dir/database.dump"
test -s "$backup_dir/database.dump"
compose exec -T postgres pg_restore --list < "$backup_dir/database.dump" > "$backup_dir/archive-list.txt"
(cd "$backup_dir" && sha256sum database.dump > SHA256SUMS)
if compose exec -T postgres psql -X -U trustcabinet -d trustcabinet \
    < deploy/scrum20-preflight.sql > "$backup_dir/preflight.txt" 2>&1; then
    cat "$backup_dir/preflight.txt"
    echo 'Archive created and readable. Restore rehearsal on an isolated database is still required.'
else
    cat "$backup_dir/preflight.txt"
    echo 'Preflight failed. Backup retained; do not merge.' >&2
    exit 2
fi
