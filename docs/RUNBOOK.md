# HAMBOX runbook — failures, backup, restore

Server: `root@168.231.85.6`, app in `/opt/hambox`. Containers: `hambox-api`, `hambox-sqlserver`, `hambox-redis`.

## First look

```bash
docker ps --format "{{.Names}} {{.Status}}"          # all three healthy?
docker logs --since 15m hambox-api | grep -iE "error|exception|fail" | tail -40
curl -s http://127.0.0.1:5000/health
```

Back office: **Operations → Dashboard** (queue depth, stuck jobs, worker heartbeat, open alerts).

## Failure playbooks

| Symptom | Likely cause | Do |
|---|---|---|
| Paid orders stay "Processing" | worker down, or supplier outage | Operations → Dashboard: heartbeat age. `docker restart hambox-api` if stale. Failed jobs → _Retry_. Orders → _Retry fulfillment_ |
| Many jobs "Failed" for one supplier | supplier outage / 429 / bad credentials | Operations → API logs for the supplier. 429s pause themselves (Retry-After). Fix credentials in Suppliers, then _Retry all_ |
| Customer paid, order still Pending | missed gateway callback | Wait ≤5 min (reconcile sweep per gateway). Else open the order → payment callbacks list; check gateway dashboard |
| Customer refunded at gateway but order not marked | settlement not recorded | Order → _Refund settlement_ → gateway reference |
| E-mails not arriving | SMTP settings / provider | Settings → Email → send test. `docker logs hambox-api | grep -i smtp` |
| Reports not e-mailed | no recipients, or SMTP | Scheduled report → executions; logs show "e-mailed to N of M" |
| Admin cannot sign in | TOTP / OTP | Use another admin to reset; see `AdminSecondFactor` settings in `appsettings.Production.json` |
| Site shows "coming soon" | store status / maintenance on | Settings → General (store status) and Maintenance |
| API unhealthy after deploy | bad migration / config | `docker logs hambox-api`; roll back with `deploy.sh` at the previous commit (see below) |

External watchdog: `scripts/worker-watchdog.sh` polls `/api/v1/system/worker-health` (header `X-Monitor-Token`,
value `Monitoring:WatchdogToken`); returns non-zero when the worker is down or jobs are stuck.

## Backup

`/opt/hambox/scripts/backup-db.sh` — full `.bak` into `/opt/hambox/backups/sql`, 14 days kept (cron). Run it by
hand before every schema change. Data-protection keys: `scripts/backup-dataprotection-keys.sh`.
**Also keep a copy of the encryption key** (`Encryption:*` in `appsettings.Production.json`): without it stored
codes cannot be decrypted, whatever the database backup says.

## Restore

```bash
# 1. stop writers
docker stop hambox-api
# 2. restore (replace file name)
docker exec hambox-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No \
  -Q "RESTORE DATABASE [HamboxDb] FROM DISK='/var/opt/mssql/backup/sql/HamboxDb_YYYYMMDD_HHMMSS.bak' WITH REPLACE"
# 3. start
docker start hambox-api
```

Then check `/health`, sign in, open a recent order. Orders paid after the backup are missing locally — reconcile
them from the gateway dashboards.

## Applying a schema change

1. `backup-db.sh`
2. copy `scripts/deploy-apply-<name>.sql` to the server, `docker cp` into `hambox-sqlserver`, run with
   `sqlcmd -b -i` (scripts are idempotent and record the EF migration id).
3. Deploy: `ssh … /opt/hambox/scripts/deploy.sh`.

## Rolling back code

On the server, in both `/opt/hambox/backend` and `/opt/hambox/frontend`: `git reset --hard <previous commit>`,
then run the build/restart steps of `deploy.sh`. Schema scripts are additive, so older code keeps working against
the newer schema.

## Environment

See `.env.example` at the repository root for every variable and what it is for (values are never committed).
