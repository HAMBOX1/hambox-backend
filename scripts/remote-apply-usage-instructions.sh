#!/usr/bin/env bash
# Run on the server: back up the database, then add the Products.UsageInstructionsHtml column (idempotent).
set -euo pipefail

/opt/hambox/scripts/backup-db.sh

SA_PASSWORD=$(grep "^SA_PASSWORD=" /opt/hambox/.env | cut -d= -f2-)
DB_NAME=$(grep "^DB_NAME=" /opt/hambox/.env | cut -d= -f2-)

docker cp /opt/hambox/tmp/deploy-apply-product-usage-instructions.sql hambox-sqlserver:/tmp/deploy-apply-product-usage-instructions.sql
docker exec hambox-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -d "$DB_NAME" -I -b -i /tmp/deploy-apply-product-usage-instructions.sql
echo "SQL applied"

docker exec hambox-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -d "$DB_NAME" -h -1 -W -Q "SELECT COL_LENGTH('catalog.Products','UsageInstructionsHtml') AS col_len; SELECT TOP 1 MigrationId FROM [catalog].[__EFMigrationsHistory] ORDER BY MigrationId DESC;"
