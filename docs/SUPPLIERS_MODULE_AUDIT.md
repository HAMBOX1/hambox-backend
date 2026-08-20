# HAMBOX Suppliers Module — Production Audit

**Date:** 2026-08-15
**Scope:** `src/Modules/Suppliers/*`, its declared integration points in Catalog/Commerce/Identity/Admin, and everywhere `Supplier`, `ISupplierProvider`, and `InventorySupplier` are referenced across the backend and frontend.
**Method:** Full read of every `.cs` file in the module, every EF configuration/migration, every cross-module reference found by repo-wide grep for `ISupplierProvider`, `ISuppliersDbContext`, `SupplierProductMapping`, `PurchaseAsync`, `ReserveAsync`, `SupplierHealthCheck`, and `InventorySupplier`; full read of the admin frontend facade/detail page/routes; a clean `dotnet build HAMBOX.slnx` (0 errors).

---

## A. Supplier architecture discovered

The Suppliers module (added after the original 4‑module audit, schema `suppliers`) is a **configuration/registry skeleton, not a fulfillment system**. It exists to let an admin register a supplier's identity, credentials, and product-code mappings, and to define a provider abstraction that a *future* automated integration would implement. As of this audit:

- **Domain** (3 entities, no state machine beyond enums):
  - `Supplier` — name/code/provider-type/base-URL/credentials/capability flags (`SupportsInventorySync`, `SupportsPriceSync`, `SupportsReservations`, `SupportsOrderStatus`, `SupportsWebhooks`), `Status` (Active/Inactive/Suspended), soft-deletable.
  - `SupplierProductMapping` — logical link from a Catalog `ProductId` to `(ExternalProductId, ExternalSku, BuyingPrice, Currency, Priority)`. No FK to Catalog (consistent with the rest of the codebase's no-cross-schema-FK convention).
  - `SupplierAuditLog` — one row per admin action (Created/Updated/Enabled/Disabled/Deleted/PriorityChanged/CredentialsUpdated/ConnectionTested/Mapping*).
- **Application**: `ISuppliersDbContext`, CQRS handlers for full CRUD + enable/disable/priority/credentials/settings on `Supplier`, full CRUD on `SupplierProductMapping`, and `TestSupplierConnectionCommand`. All 15 handlers are wired, permission-gated, and audit-logged correctly.
- **Provider abstraction**: `ISupplierProvider` declares 9 operations — `TestConnectionAsync`, `ValidateCredentialsAsync`, `SyncProductsAsync`, `SyncInventoryAsync`, `SyncPricesAsync`, `ReserveAsync`, `PurchaseAsync`, `CancelAsync`, `GetOrderStatusAsync`. **Exactly one implementation exists: `ManualSupplierProvider`.** Every method except `TestConnectionAsync`/`ValidateCredentialsAsync` returns `IsSuccess = false` with a message saying the operation isn't supported for manual suppliers — it is a deliberate "not automated" stub, not a real integration.
- **No second provider is registered.** `SuppliersInfrastructureExtensions.AddSuppliersInfrastructure` registers `ManualSupplierProvider` and nothing else. There is no HTTP client, no REST/GraphQL client, no polling/webhook receiver, anywhere in this module.
- **Presentation**: `api/v{version}/suppliers` (CRUD, provider-types, test-connection) and `api/v{version}/suppliers/{supplierId}/mappings` (CRUD). All endpoints correctly require `.RequireAuthorization()` + `.RequirePermission(PermissionConstants.Suppliers.*)`; per the platform's `PermissionAuthorizationHandler`, this already restricts them to `auth_context=Admin` + `otp_verified=true` tokens — no additional admin-context check is needed or missing.
- **Frontend**: `features/admin/suppliers` — list page, detail page (general/credentials/settings tabs), mappings page. Reuses the Admin Design System correctly (`AdminPageHeader`, `AdminSectionCard`, `AdminConfirmDialog` for delete, `AdminUnsavedChangesDialog`, `AdminLoadingSkeleton`, `AdminErrorAlert`), route-guarded per action (`Suppliers.View`/`.Create`/`.Edit`/`.Delete`/`.ManageMappings`), credential inputs use PrimeNG `PasswordModule` (masked). This part is solid and matches every convention in CLAUDE.md.

### Critical architectural finding: two unrelated "Supplier" concepts coexist

There is a **second, older, completely separate "supplier" model already live in Catalog**: `HAMBOX.Modules.Catalog.Domain.Inventory.InventorySupplier` (schema `catalog`, table presumably pre-existing from the original 4-module era). It is a plain vendor-contact record (company name, contact person, email, phone, website, country, currency, notes, `Status`) with **no credentials, no provider type, no API abstraction** — it exists purely so an admin can tag a manually-imported `InventoryBatch`/`DigitalInventoryCode` with "which vendor did we buy this from" (`InventoryBatch.SupplierId` / `DigitalInventoryCode.SupplierId` / `InventoryAuditLog.SupplierId` are nullable logical references to `InventorySupplier`, **not** to the new `Suppliers.Supplier`).

These two entities:
- Live in different schemas (`catalog` vs `suppliers`), have no FK or code relationship to each other, and are edited through **two different admin UIs** (Catalog's existing inventory-supplier management vs. the new `features/admin/suppliers`).
- Are silently conflated by the one piece of "integration" code that does exist: `SupplierHealthCheckJobHandler` (Commerce, background job type `SupplierHealthCheck`) queries `catalogDb.InventorySuppliers` — **the old entity** — and raises an `Info`-severity `SUPPLIER_INACTIVE` operational alert if any are non-Active. It never looks at the new `Suppliers.Supplier` table at all, despite the name and despite the new module having its own richer `Status`/`IsEnabled` fields.

Net effect: the only "automated" thing happening today under the "Suppliers" banner monitors the wrong table. An admin can create/disable suppliers all day in the new UI and it has **zero effect** on the one health-check job that exists, and the new `Supplier.Status`/`IsEnabled` fields are not observed by anything outside the Suppliers module itself.

**This is not something to silently "fix" by pointing the job at the new table** — that would be a behavior change requiring product sign-off on which "supplier" concept the health check is actually meant to represent (manual-inventory vendors, vs. the new API-integration registry). Flagged as a blocker-adjacent decision for product, not a bug to patch mid-audit (see §P).

---

## B. Complete supplier lifecycle (as implemented, not as designed)

| Stage | Exists? | Detail |
|---|---|---|
| Supplier creation | ✅ | `CreateSupplierCommand`, unique `Code` enforced (case-insensitive check in handler + unique DB index). |
| Supplier editing | ✅ | `UpdateSupplierCommand` (details), `UpdateSupplierSettingsCommand` (JSON blob), `UpdateSupplierCredentialsCommand`, `UpdateSupplierPriorityCommand` — all separate commands, all audited. |
| Activation/deactivation | ✅ | `Enable()`/`Disable()`/`Suspend()` on the entity; only Enable/Disable are exposed as commands — nothing calls `Suspend()` (dead domain method, no endpoint, no caller anywhere in the repo). |
| Credentials/configuration | ✅ (storage only) | Stored plaintext (see §G). No validation that credentials actually work beyond an optional manual "Test Connection" click. |
| Product mapping | ✅ | Full CRUD, but **no uniqueness enforcement** on `(SupplierId, InternalProductId)` — see §C/§K. |
| Inventory synchronization | ❌ | `SyncInventoryAsync` is declared, `ManualSupplierProvider` always returns `IsSuccess=false`, and **nothing ever calls it** — no job, no endpoint, no scheduler. |
| Purchase/order creation | ❌ | `PurchaseAsync`/`SupplierPurchaseRequest` exist only as a contract. Zero call sites in the entire backend outside the interface declaration and the stub implementation. |
| Fulfillment / code delivery via a supplier | ❌ | Does not exist. Digital code delivery is entirely handled by Catalog's existing `DigitalInventoryCode` pool (pre-existing, unrelated to this module) — confirmed by grepping the whole backend for `PurchaseAsync`/`ReserveAsync`: no hits outside the Suppliers module itself. |
| Failure / retry / timeout / cancellation | ❌ | No supplier order entity exists to have a state at all, so there is nothing to retry, time out, or cancel. `SupplierCancellationRequest`/`CancelAsync` are unused contract shapes. |
| Supplier "unavailable" state | Partial | `SupplierStatus.Suspended` exists on the entity but is unreachable through any command; `IsEnabled=false` is the only reachable "don't use this supplier" signal, and nothing reads it. |

**Bottom line: there is no supplier order lifecycle to audit for correctness, because no code anywhere creates a supplier purchase, reservation, or fulfillment record.** Every one of the "look for contradictory state" scenarios in the brief (paid-but-unfulfilled, duplicate purchase, duplicate codes from a supplier, etc.) has no code path that could produce them, because the code path that would produce them was never built. This is the single most important finding of the audit and it reframes almost every subsequent section.

---

## C. Critical findings

1. **The Suppliers module cannot fulfill an order today, at all.** `ISupplierProvider.PurchaseAsync`/`ReserveAsync` have zero callers anywhere in Commerce, Catalog, or the checkout/DOT-payment flow. If the business expectation for this delivery is "customers can be fulfilled by an external supplier," **that capability does not exist** — it is scaffolding for a future integration, not a working feature. Treat any assumption that "Suppliers" participates in checkout as false until a concrete `ISupplierProvider` implementation and a caller are written.
2. **Supplier credentials (`ApiKey`, `ApiSecret`, `Password`, `BearerToken`) are stored in plaintext** in `suppliers.Suppliers` — no `IPlatformSettingsSecretProtector`/Data Protection encryption, unlike the precedent already set by Platform Settings' SMTP password. A DB compromise or a stray backup leaks every supplier credential in clear text. DTOs correctly never return the values (only `HasApiKey`/`HasPassword`/... booleans), so the *API surface* is safe — the *storage* is not.
3. **The "SupplierHealthCheck" job monitors the wrong entity** (§A) — it is inert with respect to the new `Suppliers.Supplier` table. Any admin or on-call expectation that disabling a `Supplier` in the new UI triggers an alert or affects anything downstream is false.
4. **No SupplierProductMapping uniqueness enforcement.** `CreateSupplierMappingCommandHandler` does not check for an existing `(SupplierId, InternalProductId)` pair before insert, and the DB index on that pair is non-unique (`builder.HasIndex(m => new { m.SupplierId, m.InternalProductId })` — no `.IsUnique()`). An admin (or a retried request) can create duplicate mappings for the same product/supplier with different `Priority`/`BuyingPrice`, and nothing downstream currently reads mappings to pick "the" one, so this is latent rather than actively broken — but it will bite the first piece of code that assumes at most one active mapping per (supplier, product).

None of these are "customer got double-charged" or "customer received duplicate codes" bugs, because no code path exists that could cause those specific symptoms yet. They are correctness/security gaps in the configuration layer that exists.

---

## D. Medium/low findings

- **No optimistic-concurrency token** (`RowVersion`/`byte[]`) on `Supplier` or `SupplierProductMapping`. Two admins editing the same supplier concurrently silently last-write-wins. Low risk (admin-only, low write concurrency) but inconsistent with anywhere else in the codebase that cares about this (nowhere else in HAMBOX uses concurrency tokens either, per the architecture doc, so this is *consistent* with existing practice, not a regression — noting it only because the audit brief explicitly asked).
- **`Suspend()` is dead code** — a real domain method with no command, no endpoint, no caller. Either wire it up (a genuine "temporarily can't use this supplier but don't fully disable" state) or delete it; currently it's neither used nor documented anywhere in the admin UI (`SupplierStatus.Suspended` has no path to reach it from the frontend).
- **`BaseUrl` validation only checks "is a well-formed absolute URI"** — no scheme allowlist (http vs https), no block on loopback/link-local/cloud-metadata addresses (e.g. `169.254.169.254`). **Not currently exploitable** — `ManualSupplierProvider` never makes an HTTP call, so there is no SSRF today — but this is exactly the kind of validation that's cheap to add now and easy to forget once a real HTTP-based provider is implemented under time pressure. Flag as a pre-requisite for the first real provider, not a current vulnerability.
- **`OAuthSettingsJson` and `SettingsJson` are freeform, unvalidated JSON blobs** persisted verbatim. Consistent with the Platform Settings JSON-per-category precedent (acceptable), but note neither is schema-validated, so a malformed value only surfaces as a runtime failure inside whatever future provider tries to parse it.
- **`GetSuppliersQuery`/`GetSupplierMappingsQuery` search uses `Contains(search)`** directly against EF — fine (parameterized by EF Core, no SQL injection risk), just noting it was checked given the brief's emphasis on input handling.
- Zero automated test coverage for the module (see §M) — consistent with the rest of the codebase's stated zero-test-file state, not a regression introduced here.

---

## E. Bugs fixed

**None.** Per the audit brief's own instructions ("Do NOT start by changing code" until the audit determines what's genuinely broken, "do not perform broad refactoring," "do not create migrations unless genuinely required"), and given that:
- the module builds cleanly (0 errors),
- every finding above is either a **missing feature** (no purchase flow — a scope/product decision, not a bug to silently patch) or a **security hardening gap** (plaintext credentials, missing unique index) that would each require a migration and a explicit decision about backfilling/rotating existing data,

no code changes were made in this pass. Applying the two concrete, mechanical fixes below is low-risk and I'd recommend doing them as a small, separate follow-up commit — but did not do so unprompted, consistent with "fix genuine defects" being scoped to *this* audit's job of determining readiness, and with "never modify unrelated modules" / "large refactors are a dedicated task":

- Add `.IsUnique()` to the `(SupplierId, InternalProductId)` index (migration required).
- Encrypt `ApiKey`/`ApiSecret`/`Password`/`BearerToken` at rest via the existing `IPlatformSettingsSecretProtector` pattern (migration required to re-key existing plaintext rows — needs a decision on how to handle whatever's already stored).

## F. Tests added

**None.** There is no supplier purchase/fulfillment/idempotency logic in the codebase to write a meaningful regression test against — writing tests now would either test trivial CRUD (no genuine risk per the brief's own "do not add meaningless coverage" instruction) or would be testing code that doesn't exist yet. The highest-value test to add *today* is a CRUD-invariant test for the missing unique constraint (§C.4) if/when that fix ships.

---

## G. Security findings

- **Plaintext credential storage** (§C.2) — the standout finding. Fix path already exists in the codebase (`IPlatformSettingsSecretProtector` via ASP.NET Data Protection) and should be reused, not reinvented.
- **No credential leakage in API responses or logs found.** `SupplierDetailDto` returns only boolean presence flags; `SupplierAuditWriter`/audit log entries were checked and never serialize credential fields (audit `DetailsJson` payloads are hand-built per call site with explicit small JSON like `{"priority":100}`, never `JsonSerializer.Serialize(supplier)`).
- **Authorization is correctly enforced at the endpoint layer** for every Suppliers endpoint — no gaps found (matches the rest of HAMBOX's stated "no handler-level auth, endpoint-only" model).
- **SSRF surface is currently inert** (§D) but should be closed before any real HTTP-calling provider ships.
- Nothing in this module logs API keys, secrets, or credential values — verified by reading every `Console`/`ILogger`/audit-write call site in the module; none touch the credential properties.

---

## H. Payment / Supplier interaction findings

**There is no interaction.** Verified by grepping the entire Commerce module (including every DOT payment file: `DotPaymentGateway`, `HandleDotNotificationCommandHandler`, `HandleDotRedirectCallbackCommandHandler`, `ReconcileDotPaymentsJobHandler`, `DotPaymentVerificationService`) for any reference to `Suppliers`/`ISupplierProvider`/`SupplierId` (Suppliers-module sense) — none exist. The DOT asynchronous payment flow (pending → verifying → paid, with its own `PaymentAttempt` entity, reconciliation job, and IP allowlist for notifications) is entirely self-contained within Commerce and never calls into the Suppliers module in any way, in either direction.

Consequently, every specific proof requested in the brief's §18 ("callback alone cannot trigger fulfillment," "duplicate callbacks cannot duplicate fulfillment," "supplier failure after successful payment results in a recoverable order state," etc.) is **vacuously true for supplier fulfillment specifically**, because supplier fulfillment is not a thing that can be triggered by anything, including a payment callback. This audit does **not** claim the DOT-payment-to-order-fulfillment path itself (the non-supplier part — inventory code allocation, order state transitions) is safe or unsafe; that is Commerce/Checkout territory, out of scope for a Suppliers-module audit, and would need its own dedicated audit against `CheckoutCommandHandler`/`HandleDotNotificationCommandHandler` if the business wants that guarantee proven.

---

## I. Inventory findings

Two separate, non-interacting inventory-adjacent concepts:

1. **Catalog's real inventory** (`DigitalInventoryCode`, `InventoryBatch`) — this is what customers actually receive. It has an *optional, nullable, purely informational* `SupplierId` pointing at the old `Catalog.InventorySupplier` (label only — "who did we manually buy this from"), unrelated to the new Suppliers module. This audit did not re-verify duplicate-code/double-sell integrity for that system, as it predates and is untouched by the Suppliers module under review; CLAUDE.md's inventory-encryption migration history (`EncryptDigitalInventoryCodes`, `AddInventoryCodeRevealAudit`) suggests it has already received dedicated attention.
2. **Suppliers module's inventory awareness** — `SupplierProductMapping` only stores a `BuyingPrice`/`Currency`/external SKU reference. There is no quantity, no stock count, no reservation record anywhere in the Suppliers schema. `SyncInventoryAsync` is declared but never called. **The Suppliers module has no inventory state to corrupt, because it holds no inventory state.**

No evidence of supplier-driven double-sell, resurrected-sold-code, or batch-counter corruption risk, because no code path in this module writes to `DigitalInventoryCode`/`InventoryBatch` at all (confirmed by grep: `ISuppliersDbContext`/`SupplierProductMapping` are referenced only within the Suppliers module and the Catalog import/export job — see §J).

---

## J. Concurrency / idempotency findings

- **No idempotency mechanism exists for `PurchaseAsync`/`ReserveAsync`**, because nothing calls them. There is no idempotency key field on `SupplierProductMapping`, `Supplier`, or anywhere in the `SupplierPurchaseRequest`/`SupplierReservationRequest` contracts beyond an optional `ReferenceId string?` — a hook for a future implementation to use, currently unenforced and unvalidated at every layer (nothing generates it, nothing checks for reuse).
- **The one piece of cross-schema transactional code touching this module, `CatalogSuppliersTransactionService`**, is a correct, minimal port of the existing `ICommerceTransactionService` pattern (shares one ADO connection/transaction between `CatalogDbContext` and `SuppliersDbContext`, disables auto-savepoints, commits/rolls back atomically). It's used by Catalog's import/export background job when a package includes supplier mappings, so an import that writes both Catalog and Suppliers data can't partially commit. This is correctly built and matches the sanctioned pattern from CLAUDE.md §3 — no defect found here.
- **Admin CRUD handlers have no optimistic concurrency control** (§D) — two concurrent `PUT` requests for the same supplier last-write-wins with no conflict detection. Low real-world impact (single-admin-at-a-time editing is the realistic usage pattern) but worth knowing.
- **No unique-constraint-backed idempotency for mapping creation** (§C.4) — a double-submitted "create mapping" request (double-click, retried request after a timeout) creates two rows rather than being rejected or being a no-op.

There is no "HAMBOX sends purchase, supplier times out after actually processing, HAMBOX retries, customer double-charged/double-purchased" scenario to prove safe or unsafe, because there is no purchase call anywhere in the codebase to retry.

---

## K. Database findings

Schema `suppliers`, 3 tables (`Suppliers`, `SupplierProductMappings`, `SupplierAuditLogs`), migration `20260716215026_InitialSuppliers` — single migration, applied cleanly, matches the current model snapshot (no drift).

- **PKs**: `Id` (Guid) on all three — fine.
- **Unique constraints**: `Suppliers.Code` is correctly unique. `SupplierProductMappings.(SupplierId, InternalProductId)` is **not** unique — see §C.4, the one concrete DB-level defect found.
- **Indexes**: `Suppliers.Priority`, `Suppliers.IsDeleted`, `SupplierProductMappings.InternalProductId`, `SupplierAuditLogs.(SupplierId, CreatedOnUtc)` — all sensible for the query patterns actually used (list/sort by priority, filter by supplier, mapping lookups by product).
- **No FKs at all** between `suppliers.*` tables and `catalog.*`/`commerce.*` — consistent with the codebase-wide no-cross-schema-FK rule; `InternalProductId` on `SupplierProductMapping` is correctly treated as a logical reference only.
- **Field sizes**: credential columns are generously sized (`nvarchar(2000)`) — no obvious truncation risk for real API keys/tokens/JWTs.
- **Decimal precision**: `BuyingPrice decimal(18,2)` — consistent with the rest of the codebase's money columns; no float/double used anywhere in the module for money.
- **Soft delete**: `Supplier` implements `ISoftDeletable`/has `IsDeleted`+`IX_Suppliers_IsDeleted`; the shared `SoftDeleteInterceptor`+reflective query-filter registration (per-`DbContext.OnModelCreating`, the known duplicated pattern from CLAUDE.md §3) applies here too — confirmed `SuppliersDbContext` follows the same convention as other modules (not independently re-read line-by-line, but `Supplier : ISoftDeletable` plus the standard `AddSuppliersInfrastructure` interceptor registration is the same recipe used everywhere else, so this is presumed correct by pattern-match rather than exception).
- **`SupplierProductMapping` and `SupplierAuditLog` are not soft-deletable** — `DeleteSupplierMappingCommandHandler` does a hard `Remove()`. This is a genuine, if minor, audit-trail gap: deleting a mapping destroys the row entirely, while the corresponding `SupplierAuditLog` entry (`MappingDeleted`) only records that *a* mapping was deleted, with no snapshot of what it was (the audit detail JSON for that action is `null` — `SupplierAuditWriter.Record(dbContext, request.SupplierId, SupplierAuditAction.MappingDeleted, currentUser.UserId)` passes no `detailsJson`). If "what was this mapping before it was deleted" ever matters for a dispute/investigation, it's unrecoverable today.
- **No migration was written or applied** by this audit, per instructions.

---

## L. Background-job findings

- **`SupplierHealthCheckJobHandler`** — real, registered, wired into DI (`services.AddScoped<IBackgroundJobHandler, SupplierHealthCheckJobHandler>()`). Queries the *wrong* table (§A/§C.3). Cheap query (`CountAsync` with a `!IsDeleted && Status != Active` filter, no N+1, no unbounded load) — performance is fine, correctness/targeting is not.
- **No job exists for supplier product/inventory/price sync** — `SyncProductsAsync`/`SyncInventoryAsync`/`SyncPricesAsync` are declared on the interface and stubbed in `ManualSupplierProvider`, but no `OperationalJobType`, no recurring-job registration, and no handler exists to actually invoke them on a schedule. If the intended design is "suppliers sync automatically," that scheduler was never built.
- **No job exists for supplier order status polling / reconciliation** — no analog to `ReconcileDotPaymentsJobHandler` exists for supplier purchases, consistent with there being no supplier purchase to reconcile.
- The one cross-module job that does touch Suppliers data, Catalog's import/export package job (`ExecuteCatalogImportJobHandler`/`ExportCatalogJobHandler`), correctly scopes its supplier-mapping work behind `payload.Options.IncludeSuppliers` and only runs the shared-transaction path when there's actually supplier data in the package (`plan.SupplierMappings.Count > 0`) — not an unconditional full-catalog scan, so no obvious performance concern there.

---

## M. Build/test results

- `dotnet build HAMBOX.slnx` — **succeeded, 0 errors**, 6 pre-existing NuGet advisory warnings (`Microsoft.OpenApi` / `SQLitePCLRaw` known vulnerabilities) unrelated to this module, no new warnings introduced by Suppliers code.
- `dotnet test tests/HAMBOX.UnitTests` / `tests/HAMBOX.IntegrationTests` — not run; per CLAUDE.md and confirmed by file listing, both test projects contain zero `.cs` test files, so there is nothing to execute. This applies to Suppliers exactly as it applies to every other module — not a regression specific to this audit.
- No frontend build/lint was run for the Suppliers admin pages in this pass (no code was changed on the frontend either); the code was read and matches conventions on inspection.

---

## N. Real supplier API verification status

**BLOCKED BY SUPPLIER.** No supplier credentials, sandbox environment, or third-party API documentation were provided or found in the repository (`docs/` was not found to contain a supplier-integration spec — no `SUPPLIER_API.md`/similar). There is also no concrete third-party integration to test against: `ManualSupplierProvider` is the only registered provider and makes no network calls. Nothing in §17 of the brief (sandbox verification, idempotency mechanism, rate limits, etc.) can be attempted until a real provider is chosen and implemented. Marking this entire section **UNVERIFIED / NOT APPLICABLE** rather than fabricating any behavior.

---

## O. Remaining supplier/client dependencies

- A concrete decision on **which "supplier" concept the business actually needs**: the existing manual-inventory vendor tagging (`Catalog.InventorySupplier`, already live and in use) vs. the new API-integration registry (`Suppliers.Supplier`, currently unused by any runtime code). If the 16-day delivery window includes "suppliers work," this decision is the actual blocker, not a code defect.
- If automated integration is in scope: a real `ISupplierProvider` implementation (HTTP client, auth per `SupplierAuthenticationType`, idempotency-key handling, response validation per §7 of the brief) for at least one named supplier, plus a background job/scheduler to invoke `SyncInventoryAsync`/`SyncPricesAsync`, plus (if purchasing is in scope) a genuinely new `SupplierPurchase`/`SupplierOrder` entity with its own state machine, plus wiring that entity into Commerce's order-fulfillment path under the DOT-payment-authoritative-state guarantee the brief asks for.
- None of the above exists today even in partial/half-wired form — there's no dead half-built purchase handler to finish, it's a clean slate past the registry/CRUD layer.

---

## P. Production blockers

1. **No supplier purchase/fulfillment capability exists.** If any part of the 16-day delivery plan assumes customers can be fulfilled via an external supplier automatically, that is not buildable-on-top-of — it needs to be built from the provider layer up. This is the headline blocker.
2. **Plaintext credential storage** (§C.2) — should be fixed before any real credentials for a real supplier are ever entered into this system, since today's admin UI already invites an operator to type a real API key into a `Password`-masked-but-plaintext-stored field.
3. **`SupplierHealthCheckJobHandler` watches the wrong table** (§C.3) — needs a product decision, then a one-line fix, before it can be trusted as a monitoring signal for the new module.
4. Missing unique constraint on product mappings (§C.4) — cheap fix, should land before any bulk mapping import work begins, to avoid needing a dedup pass later.

None of these block the *rest* of the platform — Suppliers is architecturally isolated (no other module's correctness depends on it, confirmed by the cross-reference grep in §A) — so they are blockers for **shipping the Suppliers feature itself**, not for the platform at large.

---

## Q. Final readiness verdict

**Not production-ready as an automated supplier-fulfillment feature — but not because anything discovered is broken. It's because the fulfillment feature was never built.** What exists (supplier registry CRUD, credential storage, product-mapping CRUD, a stub provider, a permission-gated admin UI) is a clean, correctly-architected **configuration and extensibility scaffold** — it builds, it's permission-safe, it's audit-logged, it follows every pattern CLAUDE.md prescribes for a new module (CQRS, `Result<T>`, `I{Module}DbContext`, provider-registry-by-string-key mirroring `ICommunicationProviderRegistry`/`PaymentProviderResolver`), and the admin frontend is genuinely solid.

Answering the brief's central question directly: *"If a customer pays successfully and the supplier API times out, retries, partially succeeds, or receives the same purchase twice, can HAMBOX still guarantee correct money, inventory, order, and customer-delivery state?"* — **this cannot be tested against reality because there is no supplier API call anywhere in the codebase for a customer's payment to trigger.** Today, 100% of digital fulfillment goes through Catalog's pre-existing manual inventory pool, entirely independent of this module. That pool's own integrity was not re-audited here (out of scope for a Suppliers-module review) but is, as far as this audit can determine, untouched and unaffected by anything in the Suppliers module.

**Recommendation for the 16-day window:** treat "Suppliers" as two separate, much smaller deliverables rather than one audit-and-fix pass — (1) a quick security/DB hardening pass on the existing scaffold (encrypt credentials, add the missing unique index, decide the fate of `Suspend()` and the health-check target — all small, low-risk, no new architecture) and (2) an explicit, scoped-down decision on whether real automated supplier purchasing is actually needed for this delivery, since building it correctly (idempotency, a new order/purchase entity, DOT-payment-gated invocation, response validation) is a multi-day effort in its own right that has not been started.
