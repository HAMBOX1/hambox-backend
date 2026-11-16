#!/usr/bin/env bash
# Run on the server: back up the database, then add FloatingAds.ImageUrl / ShowCountdown (idempotent).
set -euo pipefail

/opt/hambox/scripts/backup-db.sh

SA_PASSWORD=$(grep "^SA_PASSWORD=" /opt/hambox/.env | cut -d= -f2-)
DB_NAME=$(grep "^DB_NAME=" /opt/hambox/.env | cut -d= -f2-)

docker cp /opt/hambox/tmp/deploy-apply-floating-ad-image-countdown.sql hambox-sqlserver:/tmp/deploy-apply-floating-ad-image-countdown.sql
docker exec hambox-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -d "$DB_NAME" -I -b -i /tmp/deploy-apply-floating-ad-image-countdown.sql
echo "SQL applied"

docker exec hambox-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -d "$DB_NAME" -h -1 -W -Q "SELECT COL_LENGTH('commerce.FloatingAds','ImageUrl') AS img_len, COL_LENGTH('commerce.FloatingAds','ShowCountdown') AS cd_len; SELECT TOP 1 MigrationId FROM [commerce].[__EFMigrationsHistory] ORDER BY MigrationId DESC;"
