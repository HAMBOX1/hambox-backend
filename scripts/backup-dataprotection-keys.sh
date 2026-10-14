#!/usr/bin/env bash
# Backs up the ASP.NET Core Data Protection key ring.
#
# Every encrypted value in the database (inventory codes, supplier credentials, order license keys, TOTP secrets…)
# is protected by keys in this directory. Lose the directory and those values are unrecoverable, so it is backed up
# on its own schedule, encrypted, and kept apart from the database backups.
#
# Usage (cron, daily):  /opt/hambox/scripts/backup-dataprotection-keys.sh
#
# Configuration (environment or defaults):
#   KEYRING_DIR      key ring directory      (default /opt/hambox/data/uploads/dataprotection-keys)
#   BACKUP_DIR       where archives are put   (default /opt/hambox/backups/keyring)
#   PASSPHRASE_FILE  file holding the passphrase, mode 600  (default /opt/hambox/secrets/keyring-backup.pass)
#   RETENTION_DAYS   how long archives are kept (default 30)
#
# Restore:
#   openssl enc -d -aes-256-cbc -pbkdf2 -pass file:$PASSPHRASE_FILE -in keyring-YYYYmmdd-HHMMSS.tar.gz.enc | tar -xz -C /opt/hambox/data/uploads
#
# Keep a copy of the passphrase OUTSIDE this server (password manager) — an archive is useless without it.

set -euo pipefail

KEYRING_DIR="${KEYRING_DIR:-/opt/hambox/data/uploads/dataprotection-keys}"
BACKUP_DIR="${BACKUP_DIR:-/opt/hambox/backups/keyring}"
PASSPHRASE_FILE="${PASSPHRASE_FILE:-/opt/hambox/secrets/keyring-backup.pass}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"

if [ ! -d "$KEYRING_DIR" ]; then
  echo "key ring directory not found: $KEYRING_DIR" >&2
  exit 1
fi

if [ ! -s "$PASSPHRASE_FILE" ]; then
  echo "passphrase file missing or empty: $PASSPHRASE_FILE" >&2
  echo "create it once:  umask 077; head -c 48 /dev/urandom | base64 > $PASSPHRASE_FILE" >&2
  exit 1
fi

mode="$(stat -c '%a' "$PASSPHRASE_FILE")"
if [ "$mode" != "600" ] && [ "$mode" != "400" ]; then
  echo "refusing to use $PASSPHRASE_FILE: mode is $mode, expected 600" >&2
  exit 1
fi

key_count="$(find "$KEYRING_DIR" -maxdepth 1 -name 'key-*.xml' | wc -l)"
if [ "$key_count" -eq 0 ]; then
  echo "no key-*.xml files in $KEYRING_DIR — nothing to back up" >&2
  exit 1
fi

umask 077
mkdir -p "$BACKUP_DIR"
stamp="$(date -u +%Y%m%d-%H%M%S)"
target="$BACKUP_DIR/keyring-$stamp.tar.gz.enc"

tar -czf - -C "$(dirname "$KEYRING_DIR")" "$(basename "$KEYRING_DIR")" \
  | openssl enc -aes-256-cbc -pbkdf2 -salt -pass "file:$PASSPHRASE_FILE" -out "$target"

# Verify the archive decrypts and lists before trusting it.
openssl enc -d -aes-256-cbc -pbkdf2 -pass "file:$PASSPHRASE_FILE" -in "$target" | tar -tz >/dev/null

find "$BACKUP_DIR" -name 'keyring-*.tar.gz.enc' -mtime "+$RETENTION_DAYS" -delete

echo "key ring backed up: $target ($key_count keys)"
