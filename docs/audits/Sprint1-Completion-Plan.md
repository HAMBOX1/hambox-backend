# Sprint 1 Completion Plan — P0 Backlog (Contract-Aligned)

**Source Audit:** [`docs/audits/Sprint1-Compliance-Audit.md`](Sprint1-Compliance-Audit.md)  
**Signed Contract:** `c:\Users\Royal\Desktop\عقد تطوير منصة إلكترونية لبيع المنتجات الرقمية.pdf`  
**Plan Date:** 23 June 2026  
**Status:** Planning only — **no implementation in this document**  
**Target Outcome:** Sprint 1 = **100% complete** per signed contract (Stage 1 — 15% / EGP 16,000)

---

## Contract Source of Truth — Sprint 1 Deliverables

Extracted from **Section 7 — خطة التنفيذ (Sprints)**, pages 10–11 of the signed contract:

| # | Contract Item (Arabic) | English | In Codebase Today |
|---|------------------------|---------|-------------------|
| 1 | إعداد Architecture المشروع | Project architecture setup | ✅ Modular monolith, Clean Architecture |
| 2 | إعداد قاعدة البيانات | Database setup | ✅ SQL Server, migrations, schemas |
| 3 | نظام Authentication | Authentication system | ⚠️ Flows exist; email delivery is stub |
| 4 | نظام الصلاحيات | Authorization / permissions system | ⚠️ Policies + seeds; no admin UI/API |
| 5 | Product CRUD | Product CRUD | ✅ Complete |
| 6 | إعداد الواجهة الأساسية | Foundation UI setup | ❌ No Angular frontend in solution |
| 7 | تجهيز نسختين (المحلية + الدولية) | Local + international version prep | ⚠️ Bilingual fields only; no i18n infra |
| 8 | إعداد البنية التحتية البرمجية | Development infrastructure preparation | ✅ Docker, Serilog, health, OpenAPI (dev) |

**Sprint 1 acceptance scope (15%):** Architecture + Foundation UI/UX Design + System Architecture + Environment Setup + Project Initialization + Development Infrastructure Preparation + Foundation UI.

**Contract tech stack (Section 6):** ASP.NET Core Web API · **Angular** · SQL Server · Redis Queue (later) · Modular Monolith · VPS Linux.

**Critical contract note — Sprint 2 explicitly includes Admin Role Management:**

> Sprint 2: *نظام Authentication، نظام الصلاحيات الأساسي وإدارة الأدوار (Admin Role Management). Product CRUD…*

Therefore **Role Management REST APIs** and **Permission Management admin APIs** from the code audit are **Sprint 2 deliverables**, not Sprint 1 sign-off blockers per the signed contract.

---

## Audit vs Contract Reconciliation

| Audit P0 Item | Audit Status | Contract Sprint | Revised Priority |
|---------------|--------------|-----------------|------------------|
| Role Management REST API | 55% — no admin APIs | **Sprint 2** (Admin Role Management) | **Deferred → Sprint 2** |
| Permission Management REST API | 55% — no admin APIs | **Sprint 2** (with Admin Role Management) | **Deferred → Sprint 2** |
| Real email delivery | 92% — logging stub | **Sprint 1** (Authentication) | **P0-1** |
| Integration tests | 0% — empty test projects | **Sprint 1** (client tests each sprint — Clause 3) | **P0-4** |
| Localization infrastructure | 65% — bilingual fields only | **Sprint 1** (تجهيز نسختين) | **P0-2** |
| Foundation UI (Angular) | Not audited | **Sprint 1** (إعداد الواجهة الأساسية) | **P0-3** |
| Permissions system (runtime) | 78% — policies work | **Sprint 1** (نظام الصلاحيات) | **P0-5** (minor closure) |

---

## Revised P0 Scope (Contract Sign-off)

| ID | P0 Item | Contract Reference | Current % | Target % | Effort |
|----|---------|-------------------|-----------|----------|--------|
| **P0-1** | Real email delivery service | Authentication system | 92% | 100% | 1–2 days |
| **P0-2** | Localization foundation (AR/EN, RTL/LTR) | تجهيز نسختين | 65% | 100% | 3–4 days |
| **P0-3** | Foundation Angular UI | إعداد الواجهة الأساسية | 0% | 100% | 5–7 days |
| **P0-4** | Integration / smoke tests | Sprint acceptance (Clause 3) | 0% | 100% | 3–4 days |
| **P0-5** | Authorization system closure | نظام الصلاحيات | 78% | 100% | 1 day |

**Total revised effort:** **13–18 dev-days (~3 weeks)**

### Sign-off projection

| Metric | Before | After all P0 |
|--------|--------|--------------|
| Contract Sprint 1 items (8) | ~6/8 partial | **8/8 complete** |
| Overall Sprint 1 (contract) | ~75% | **100%** |
| Audit expanded checklist (24 items) | ~87% | ~92% (role admin APIs remain Sprint 2) |

---

## Dependency Graph (Contract P0)

```
┌──────────────────────────────────────────────────────────────────┐
│  P0-1 Email Delivery              (start Day 1 — no deps)       │
└────────────────────────────┬─────────────────────────────────────┘
                             │
┌────────────────────────────┼─────────────────────────────────────┐
│  P0-2 Localization API      │  P0-3 Angular Foundation UI          │
│  (middleware, culture)      │  (new repo folder, i18n, auth UI)   │
│  start Day 1 parallel       │  start Day 2 (needs API base URL)     │
└────────────────────────────┼─────────────────────────────────────┘
                             │
┌────────────────────────────▼─────────────────────────────────────┐
│  P0-5 Authorization closure (docs, seed SuperAdmin test path)    │
└────────────────────────────┬─────────────────────────────────────┘
                             │
┌────────────────────────────▼─────────────────────────────────────┐
│  P0-4 Integration + smoke tests (all Sprint 1 surfaces)           │
└──────────────────────────────────────────────────────────────────┘
```

**Execution order:** P0-1 ∥ P0-2 → P0-3 → P0-5 → P0-4

---

# P0-1 — Real Email Delivery Service

## Why it is incomplete

`RegisterCommandHandler` and `ForgotPasswordCommandHandler` call `IEmailService`, but the only implementation logs to Serilog. Users cannot complete email verification or password reset — the Authentication system is non-functional for real users.

### Affected projects and files

| Project | Files |
|---------|-------|
| `HAMBOX.Modules.Identity.Application` | `Abstractions/IEmailService.cs`, `Options/EmailSettings.cs` *(new)* |
| `HAMBOX.Modules.Identity.Infrastructure` | `Services/EmailService.cs` *(refactor)*, `Services/SmtpEmailService.cs` *(new)*, `Services/LoggingEmailService.cs` *(new)*, `Templates/*` *(new)*, `Extensions/IdentityInfrastructureExtensions.cs` |
| `HAMBOX.API` | `appsettings.json`, `appsettings.Development.json` |
| Root | `docker-compose.yml`, `docker-compose.override.yml`, `.env`, `README.md`, `Directory.Packages.props` |

### Implementation plan

| Step | Task | Details |
|------|------|---------|
| 1 | Add `EmailSettings` + `AppSettings:BaseUrl` | SMTP host/port/SSL/credentials; `Enabled` flag for fallback |
| 2 | Add `MailKit` package | `Directory.Packages.props` + Infrastructure csproj |
| 3 | Implement `SmtpEmailService` | HTML templates with `{BaseUrl}/api/auth/verify-email?token={token}` |
| 4 | Refactor `LoggingEmailService` | Preserve current log-only behavior when `Enabled=false` |
| 5 | Conditional DI | `IdentityInfrastructureExtensions.cs` |
| 6 | Add Mailpit to Docker Compose | Ports `8025` (UI), `1025` (SMTP) |
| 7 | Document in README | Local, Docker, production env vars |

### Acceptance criteria

- [ ] Registration sends verifiable email (visible in Mailpit locally)
- [ ] Forgot-password sends reset token email
- [ ] `EmailSettings:Enabled=false` preserves log-only dev mode
- [ ] No secrets committed to source control

### Effort: **1–2 days** · **Depends on:** nothing

---

# P0-2 — Localization Foundation (Local + International Versions)

## Why it is incomplete

Contract Sprint 1 requires **تجهيز نسختين (المحلية + الدولية)**. MVP scope (Contract Section 2) further requires Arabic/English setup, language switching, main interface translation, RTL/LTR, and dynamic multilingual content.

The codebase has **bilingual data fields** (`NameAr`/`NameEn`) but no localization infrastructure: no `Accept-Language` middleware, no `.resx` or JSON locale files, no culture negotiation, no RTL layout support.

### Affected projects and files

| Project | Files |
|---------|-------|
| `HAMBOX.Infrastructure` | `Middleware/LocalizationMiddleware.cs` *(new)*, `Extensions/InfrastructureExtensions.cs`, `Extensions/LocalizationExtensions.cs` *(new)* |
| `HAMBOX.API` | `Program.cs`, `appsettings.json` |
| `HAMBOX.Modules.Catalog.Application` | `Contracts/ProductDto.cs`, `Contracts/CategoryDto.cs`, query handlers *(optional localized projection)* |
| `HAMBOX.Modules.Catalog.Presentation` | `Endpoints/ProductEndpoints.cs`, `Endpoints/CategoryEndpoints.cs` |
| Root | `README.md` — document local vs international deployment |
| **New:** `frontend/hambox-web/` | Angular i18n shares this epic with P0-3 |

### Implementation plan

| Step | Task | Details |
|------|------|---------|
| 1 | Add ASP.NET Core localization | `AddLocalization()`, supported cultures `ar`, `en`, default `ar` |
| 2 | `Accept-Language` middleware | Read header; set `CultureInfo.CurrentCulture`; echo in response |
| 3 | Shared API messages | `Resources/SharedResources.ar.resx`, `SharedResources.en.resx` in API or SharedKernel |
| 4 | Optional: localized DTO projection | Add `LocalizedProductDto` with single `Name`/`Description` based on culture, or document client-side selection of `NameAr`/`NameEn` |
| 5 | CORS + culture headers | Allow `Accept-Language` in preflight |
| 6 | Document deployment profiles | `appsettings.Local.json` (ar default), `appsettings.International.json` (en default) |
| 7 | Angular i18n (with P0-3) | `@angular/localize`, `assets/i18n/ar.json`, `assets/i18n/en.json`, RTL via `dir="rtl"` |

### Acceptance criteria

- [ ] API respects `Accept-Language: ar` and `Accept-Language: en`
- [ ] ProblemDetails / validation messages can be localized
- [ ] README documents local (AR) vs international (EN) configuration
- [ ] Angular app switches language and layout direction (with P0-3)

### Effort: **3–4 days** (API: 1–2 days; shared with P0-3 for UI) · **Depends on:** nothing (parallel with P0-1)

---

# P0-3 — Foundation Angular UI

## Why it is incomplete

Contract Section 6 mandates **Frontend: Angular**. Sprint 1 explicitly requires **إعداد الواجهة الأساسية** and acceptance includes **Foundation UI/UX Design**.

The solution contains **only the ASP.NET Core API** (`HAMBOX.API`). There is no `frontend/` or Angular project in `HAMBOX.slnx`. CORS is pre-configured for `localhost:3000` but no client exists.

### Affected projects and files

| Project | Files |
|---------|-------|
| **New** `frontend/hambox-web/` | Entire Angular application |
| `HAMBOX.slnx` | Add frontend folder reference (or separate repo per team convention) |
| `HAMBOX.API` | `appsettings.json` CORS origins, `Program.cs` |
| Root | `README.md`, optional `docker-compose.yml` frontend service |

### Implementation plan

| Step | Task | Details |
|------|------|---------|
| 1 | Scaffold Angular 19+ app | `ng new hambox-web --routing --style=scss --ssr=false` |
| 2 | Project structure | `core/` (auth, api, interceptors), `shared/`, `features/auth`, `features/catalog`, `layouts/` |
| 3 | Environment config | `environment.ts` → `apiUrl: http://localhost:5000` |
| 4 | Auth interceptor | Attach JWT Bearer token from localStorage |
| 5 | Auth pages | Login, Register, Verify Email, Forgot/Reset Password — call existing `/api/auth/*` |
| 6 | Catalog pages (read-only foundation) | Product list, product detail, category list — call `/api/v1/*` |
| 7 | i18n + RTL | `@angular/localize`, AR/EN toggle, `document.dir` switching |
| 8 | Base layout | Header, footer, language switcher, responsive shell (Mobile First per contract Section 13) |
| 9 | API service layer | Typed HTTP services matching OpenAPI/Swagger spec |
| 10 | Docker dev profile (optional) | `ng serve` or nginx container in compose |

### Minimum pages for Sprint 1 sign-off

| Page | API Endpoint | Auth |
|------|--------------|------|
| Login | `POST /api/auth/login` | Public |
| Register | `POST /api/auth/register` | Public |
| Verify Email | `POST /api/auth/verify-email` | Public |
| Product List | `GET /api/v1/products` | Public |
| Category List | `GET /api/v1/categories` | Public |

Admin/role management UI is **Sprint 2** per contract.

### Acceptance criteria

- [ ] Angular app runs locally and connects to API
- [ ] User can register → verify email (with P0-1) → login
- [ ] Products and categories display in AR and EN
- [ ] RTL layout works for Arabic
- [ ] README includes frontend setup instructions

### Effort: **5–7 days** · **Depends on:** P0-1 (email), P0-2 (i18n coordination)

---

# P0-4 — Integration & Smoke Tests

## Why it is incomplete

Contract **Clause 3 (اختبارات المشروع)** states the client may test the system before accepting each Sprint. Test projects exist but contain **zero test source files**.

### Affected projects and files

| Project | Files |
|---------|-------|
| `HAMBOX.IntegrationTests` | `HAMBOX.IntegrationTests.csproj`, all `*Tests.cs` *(new)* |
| `HAMBOX.API` | `Program.cs` — add `public partial class Program { }` |
| `Directory.Packages.props` | `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.MsSql`, `FluentAssertions` |

### Implementation plan

| Step | Task | Details |
|------|------|---------|
| 1 | Add test packages | Central package management |
| 2 | `HamboxWebApplicationFactory` | Testcontainers SQL Server; apply migrations; override JWT secret |
| 3 | `FakeEmailService` | Capture verification/reset tokens for test assertions |
| 4 | **Auth flow tests** | Register → verify → login → refresh → logout |
| 5 | **Catalog tests** | Product/category CRUD; pagination; search; permission enforcement on writes |
| 6 | **Localization smoke test** | `Accept-Language` header returns expected behavior |
| 7 | **Health check test** | `GET /health` returns healthy |
| 8 | Document `dotnet test` | README + Docker requirement for Testcontainers |

### Test cases (minimum 15)

| # | Test | Sprint 1 Contract Area |
|---|------|------------------------|
| 1–7 | Full auth lifecycle | Authentication |
| 8–10 | Catalog GET (public) + POST (authorized) | Product CRUD |
| 11 | Category CRUD smoke | Catalog (bonus — not explicit Sprint 1) |
| 12 | Permission denied without JWT | نظام الصلاحيات |
| 13 | SuperAdmin/content-manager can mutate catalog | نظام الصلاحيات |
| 14 | `Accept-Language` middleware | تجهيز نسختين |
| 15 | `/health` | Infrastructure |

Role/permission **admin API tests** deferred to Sprint 2.

### Acceptance criteria

- [ ] ≥ 15 integration tests passing
- [ ] `dotnet test` green with Docker
- [ ] Tests runnable before Sprint 1 client acceptance demo

### Effort: **3–4 days** · **Depends on:** P0-1, P0-2, P0-5 (P0-3 UI tested manually or via Cypress in Sprint 2)

---

# P0-5 — Authorization System Closure

## Why it is incomplete

Contract Sprint 1 requires **نظام الصلاحيات** (permissions system), not full admin role management (Sprint 2). Runtime authorization is largely implemented but has operational gaps:

- No documented path for an admin user to obtain catalog mutation permissions
- `Customer` role has zero permissions until email verified; no seed admin account for demos
- `Roles.Manage` permission exists but no API yet (acceptable for Sprint 1)

### Affected projects and files

| Project | Files |
|---------|-------|
| `HAMBOX.Modules.Identity.Infrastructure` | `Configurations/ApplicationRoleConfiguration.cs`, optional dev seed user |
| `HAMBOX.Modules.Identity.Application` | `Features/VerifyEmail/VerifyEmailCommandHandler.cs` |
| `HAMBOX.API` | `appsettings.Development.json` |
| `README.md` | Document permission model, seeded roles, how to test protected endpoints |

### Implementation plan

| Step | Task | Details |
|------|------|---------|
| 1 | Dev seed admin user (Development only) | Insert `SuperAdmin` user with known password + `UserRole` mapping in migration or startup seed |
| 2 | Document permission matrix | README table: role → permissions |
| 3 | Verify JWT carries permission claims | Confirm `UserClaimsService` + `PermissionAuthorizationHandler` for catalog mutations |
| 4 | Swagger auth instructions | How to authenticate and call protected endpoints |
| 5 | Optional: assign `ContentManager` on verify-email for demo | Business decision — not required if SuperAdmin seed exists |

### Acceptance criteria

- [ ] Developer can login as seeded admin and create products/categories
- [ ] Unauthorized requests return 401/403
- [ ] Permission model documented for client acceptance demo
- [ ] No Sprint 2 admin APIs required for this item

### Effort: **1 day** · **Depends on:** P0-1 (verified users can login)

---

# Master Task List (Dependency-Ordered)

| Order | ID | Task | Effort | Depends On |
|-------|-----|------|--------|------------|
| 1 | T-01 | EmailSettings + SmtpEmailService + Mailpit Docker | 1d | — |
| 1 | T-02 | ASP.NET Core localization middleware + resources | 1d | — |
| 2 | T-03 | README email + localization docs | 0.5d | T-01, T-02 |
| 2 | T-04 | Scaffold Angular app + environment + API client | 1d | — |
| 3 | T-05 | Angular auth pages wired to API | 2d | T-01, T-04 |
| 3 | T-06 | Angular catalog pages + i18n/RTL | 2d | T-02, T-04 |
| 4 | T-07 | Dev SuperAdmin seed + permission docs | 1d | T-01 |
| 5 | T-08 | WebApplicationFactory + FakeEmailService | 1d | T-01, T-07 |
| 5 | T-09 | Auth + catalog integration tests | 2d | T-08 |
| 5 | T-10 | Localization + health smoke tests | 0.5d | T-02, T-08 |
| 6 | T-11 | Client acceptance dry-run + contract checklist | 0.5d | All |

**Total: 13–18 dev-days**

---

# Deferred to Sprint 2 (Per Contract)

The following were **audit P0** items but are **explicitly Sprint 2** in the signed contract. Retain the detailed design from the original audit plan for Sprint 2 kickoff.

| Item | Contract Reference | Current % | Sprint 2 Target |
|------|-------------------|-----------|-----------------|
| Admin Role Management REST API | Sprint 2 — إدارة الأدوار | 55% | 100% |
| Permission admin APIs (role-permission mapping) | Sprint 2 — نظام الصلاحيات الأساسي | 55% | 100% |
| Draft Auto-Save | Sprint 2 | 0% | 100% |
| Digital codes / inventory | Sprint 2 | 0% | 100% |

**Sprint 2 detailed implementation** (from codebase analysis — implement in Sprint 2):

- `Features/Roles/*` — 8 endpoints under `api/v1/roles` and `api/v1/users/{id}/roles`
- `Features/Permissions/*` — 3 endpoints for list + role-permission update
- Domain: `ApplicationRole.Update()`, `Delete()`, `SetPermissions()`
- Presentation: `RoleEndpoints.cs`, `PermissionEndpoints.cs`
- See original role/permission file list in git history of this document if needed

---

# Definition of Done — Sprint 1 Contract Sign-off

| Contract Item | Done When |
|---------------|-----------|
| Architecture | Modular monolith builds; documented in README |
| Database | Migrations apply; Identity + Catalog schemas live |
| Authentication | Register/login/refresh/verify/forgot/reset work with **real email** |
| Permissions system | JWT policies enforce catalog mutations; admin demo path documented |
| Product CRUD | API CRUD complete; visible in Angular product pages |
| Foundation UI | Angular app with auth + catalog foundation pages |
| Local + International | AR/EN switching, RTL/LTR, bilingual data |
| Dev infrastructure | Docker Compose runs API + SQL + Mailpit; logging; health |
| Client acceptance | Integration tests green; demo script for 15% payment milestone |

### Final verdict after P0 completion

# SPRINT 1 COMPLETE (per signed contract)

---

# Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Angular scope creep | Delays Sprint 1 | Strict minimum page list; admin UI in Sprint 2 |
| No UI designer deliverable | Foundation UI/UX acceptance unclear | Use contract wording "إعداد" (setup), not full design system |
| Frontend in same repo vs separate | Team coordination | Add `frontend/` to monorepo; document in README |
| Testcontainers needs Docker | CI friction | Document requirement; LocalDB fallback |
| Audit vs contract mismatch | Wrong priorities | **This plan uses contract as authority** |

---

*End of Sprint 1 Completion Plan (Contract-Aligned)*
