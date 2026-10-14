# Payment gateway decisions — for client sign-off

These two items are **contractual decisions, not code**. The platform implements them as described below; what is
still missing is the client's written acceptance. Please review, then sign (or reply with an approving e-mail and
attach it here). Nothing in this document has been approved yet.

---

## D-1 — DOT (and DOT Fawry) instead of Kashier / Banque Misr

**Contract reference:** Scope §14.1 names Kashier and Banque Misr; it also allows "any other gateway specified
technically".

**What is built and live**

| Method | Gateway | How it is verified |
|---|---|---|
| Orange Cash, Vodafone Cash | DOT (OTP redirect) | Every callback is re-checked against DOT's Check-Transaction-Status API before any order is fulfilled |
| Fawry | DOT Fawry Direct Billing | Same server-to-server re-check; webhook + reconciliation job |

**Why DOT**

- Local Egyptian wallets and Fawry — the payment methods the target customers actually use.
- Hosted checkout: HAMBOX never sees, stores or processes card data (Scope §14 / R27).
- Amount and currency are verified against the order before delivery; a mismatch fails the order.

**Decision requested:** accept DOT / DOT Fawry as the payment gateway for the Egyptian market in place of Kashier /
Banque Misr, or instruct that Kashier / Banque Misr be added as an additional gateway (separate estimate).

| | |
|---|---|
| Decision | ☐ Accepted   ☐ Add Kashier   ☐ Add Banque Misr |
| Name / role | |
| Date | |
| Signature or reference to approving e-mail | |

---

## D-2 — Crypto (Cryptomus, OxaPay) as the "international gateway"

**Contract reference:** the scope calls for a gateway that serves international customers.

**What is built and live**

- Two independent crypto gateways: **Cryptomus** and **OxaPay**, both invoicing in USD, hosted checkout, signed
  webhooks plus server-to-server status re-verification, both selectable and individually enabled from the admin
  dashboard (Payment Gateways).
- Per-gateway fee percentage, configurable by the admin.
- Every webhook (accepted or rejected) is logged with its signature result, gateway reference, raw payload and
  processing outcome (Admin → Orders → payment webhooks).

**What is NOT built**

- No card-based international gateway (Stripe / PayPal / Checkout.com). Crypto is the only international method.
- No separate "second platform build" per market: one deployment serves every market; gateways are switched on and
  off by the admin and shown to every customer. If a separate international build or per-country gateway
  availability is required, that is additional scope.

**Decision requested:** confirm that crypto satisfies the international-gateway requirement, or specify the
additional international gateway / per-market configuration that is required.

| | |
|---|---|
| Decision | ☐ Crypto accepted as the international gateway   ☐ Additional gateway required: ______________ |
| Name / role | |
| Date | |
| Signature or reference to approving e-mail | |

---

## D-3 — Queue system: database-backed queue instead of Redis Queue

**Contract reference:** the stack lists "Queue System: Redis Queue"; the scope accepts a persistent queue with an
independent worker.

**What is built**

- A persistent, SQL-backed job queue (`OperationalJobs`): jobs survive restarts, are claimed with optimistic
  concurrency (two workers can never run the same job), retried on a schedule (default 0 / 2 / 5 / 15 minutes with
  jitter), dead-lettered after the threshold, and visible/retryable from the admin Operations screens.
- Redis is used only as an optional low-latency "wake the worker" signal; the SQL row is always the source of truth.
- Per-order jobs are enqueued at most once while one is waiting (idempotent enqueue), and every handler is itself
  idempotent (supplier purchase claim, unique manual code).

**Known limits**

- The worker runs inside the API process, so an API restart pauses it until the API is back (queued jobs are not lost).
- Moving to Redis Queue / Hangfire, or to a separately deployed worker service, is a change to the stack the contract
  names and needs the client's written approval.

| | |
|---|---|
| Decision | ☐ DB-backed queue accepted   ☐ Redis Queue required (separate estimate)   ☐ Separate worker service required |
| Name / role | |
| Date | |
| Signature or reference to approving e-mail | |
