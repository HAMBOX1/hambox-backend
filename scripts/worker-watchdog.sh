#!/usr/bin/env bash
# External watchdog for the HAMBOX background worker.
#
# The worker runs inside the API process, so "the worker stopped" can never be reported by the worker itself. Run
# this from cron (every 2 minutes) — it asks the API from the outside and shouts when the answer is not 200.
#
#   */2 * * * * /opt/hambox/scripts/worker-watchdog.sh >> /opt/hambox/logs/worker-watchdog.log 2>&1
#
# Configuration (environment or /opt/hambox/.env):
#   WATCHDOG_URL       default http://localhost:5000/api/v1/system/worker-health
#   WATCHDOG_TOKEN     must equal Monitoring:WatchdogToken in the API config
#   ALERT_WEBHOOK_URL  optional: a Slack/Telegram/Discord-compatible webhook that receives a JSON {"text": "..."}
#   STATE_FILE         remembers the last state so an outage is announced once, and its recovery once
set -uo pipefail

ENV_FILE="${ENV_FILE:-/opt/hambox/.env}"
if [ -f "$ENV_FILE" ]; then
  # shellcheck disable=SC1090
  set -a; . "$ENV_FILE"; set +a
fi

URL="${WATCHDOG_URL:-http://localhost:5000/api/v1/system/worker-health}"
TOKEN="${WATCHDOG_TOKEN:-}"
STATE_FILE="${STATE_FILE:-/tmp/hambox-worker-watchdog.state}"
stamp() { date -u +%Y-%m-%dT%H:%M:%SZ; }

notify() {
  echo "$(stamp) $1"
  if [ -n "${ALERT_WEBHOOK_URL:-}" ]; then
    curl -s -m 10 -H 'Content-Type: application/json' -d "{\"text\": \"$1\"}" "$ALERT_WEBHOOK_URL" >/dev/null || true
  fi
}

body="$(mktemp)"
code="$(curl -s -m 15 -o "$body" -w '%{http_code}' -H "X-Monitor-Token: $TOKEN" "$URL" || echo 000)"
previous="$(cat "$STATE_FILE" 2>/dev/null || echo ok)"

if [ "$code" = "200" ]; then
  if [ "$previous" != "ok" ]; then
    notify "HAMBOX worker RECOVERED (health check returned 200)."
  fi
  echo ok > "$STATE_FILE"
else
  detail="$(tr -d '\n' < "$body" | cut -c1-300)"
  if [ "$previous" != "down" ]; then
    notify "HAMBOX worker health check FAILED (HTTP $code): $detail"
  else
    echo "$(stamp) still failing (HTTP $code)"
  fi
  echo down > "$STATE_FILE"
fi

rm -f "$body"
