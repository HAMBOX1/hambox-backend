# Test report

_Run on 2026-10-16 against the code deployed that day._

## Automated suites (all executed, this is the real output)

| Suite | Result |
|---|---|
| Backend unit tests (`tests/HAMBOX.UnitTests`) | **1110 passed**, 0 failed |
| Frontend tests (`ng test`) | **230 passed**, 0 failed (67 files) |
| Backend integration tests (`tests/HAMBOX.IntegrationTests`, LocalDB) | 131 of 133 passed in the full run; the failures were LocalDB connection errors (`pre-login handshake`, pipe closed) in auth tests — a different test each run, so an environment fault on the test machine rather than a logic failure. The same tests were not re-confirmed green in a single clean run |
| Frontend production build (`ng build`) | succeeds |

## What the automated tests cover

Payment verification and reconciliation for all four gateways, stock holds under concurrency, purchase controls
and order holds, refund settlement, job retry schedule / jitter / dedupe, supplier 429 + Retry-After + throttle,
supplier availability freshness, manual-action audit (reasons, old/new, origin), worker watchdog and alert
delivery, fail-soft notifications, referral anti-abuse, review votes and moderation, activity log + CSV (formula
injection), sitemap/robots, scheduled-report e-mail, bilingual legal seeding, GA/Meta consent gating and
once-per-order Purchase, library reveal flow, order-confirmation behaviour.

## Not covered by automated tests — must be checked by hand

These need real third-party credentials or a browser and were **not** exercised:

1. **Live gateway sandbox payments** (DOT, DOT-Fawry, Cryptomus, OxaPay): one successful and one failed payment per
   gateway, callback + reconcile.
2. **Live supplier fulfillment** per supplier account.
3. **GA4 DebugView / Meta Pixel Helper** — needs the real measurement ids entered in Settings → Analytics.
4. **Visual RTL / responsive check** of the storefront and back office in a real browser.
5. **Search-engine side**: submit `/sitemap.xml` in Search Console; check structured data with Google's Rich
   Results Test.
6. **Scheduled report e-mail** with the production SMTP account.
7. **Admin two-factor enrolment** on production (see Admin guide).

## Known limits

- The storefront is a client-rendered SPA: meta tags and JSON-LD are set after the app loads. Crawlers that run
  JavaScript (Google, Bing) see them; link-preview bots that do not (some social scrapers) fall back to the
  static `index.html`. Server-side prerendering is not implemented.
- Gateway refunds are recorded manually (the money is returned in the gateway dashboard).
