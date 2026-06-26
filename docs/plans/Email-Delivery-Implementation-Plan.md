# Email Delivery Implementation Plan — HAMBOX

**Status:** Awaiting approval — **no code implemented yet**  
**Date:** 23 June 2026  
**Scope:** Production-ready SMTP email for Identity transactional flows  
**Contract / Audit link:** Sprint 1 Blocker — Authentication system

---

## 1. Objective

Replace the logging-only `EmailService` with a **MailKit SMTP** implementation that delivers:

| Flow | Trigger | Current consumer |
|------|---------|------------------|
| Email verification | `RegisterCommandHandler` | `SendEmailVerificationAsync` |
| Password reset | `ForgotPasswordCommandHandler` | `SendPasswordResetAsync` |

**Constraints (from request):**

- Keep existing `IEmailService` contract unchanged
- Read SMTP settings from `appsettings.json` (+ env overrides)
- Development fallback via **Mailpit** in Docker Compose
- Preserve Clean Architecture layer boundaries
- Add startup validation and structured Serilog logging

---

## 2. Current State (Evidence)

| Item | Location | State |
|------|----------|-------|
| Contract | `IEmailService.cs` | Two methods; no changes planned |
| Stub impl | `EmailService.cs` | Logs only; returns `Task.CompletedTask` |
| DI registration | `IdentityInfrastructureExtensions.cs:126` | `AddScoped<IEmailService, EmailService>()` |
| Register flow | `RegisterCommandHandler.cs:50-55` | Saves user, then calls email |
| Forgot-password flow | `ForgotPasswordCommandHandler.cs:39-44` | Saves token, then calls email |
| Verify endpoint | `AuthEndpoints.cs:86-94` | `POST /api/auth/verify-email?token=` |
| Reset endpoint | `AuthEndpoints.cs:106-114` | `POST /api/auth/reset-password` body `{ token, newPassword }` |
| Options pattern | `JwtSettings.cs`, `LockoutSettings.cs` | Application-layer options + Infrastructure validator |
| Docker | `docker-compose.yml` | SQL Server + API only; **no Mailpit** |
| Packages | `Directory.Packages.props` | **No MailKit** |

**Handlers require no signature changes** if `IEmailService` stays the same.

---

## 3. Architecture Design

### 3.1 Layer responsibilities

```
┌─────────────────────────────────────────────────────────────┐
│  HAMBOX.Modules.Identity.Application                        │
│  ├── Abstractions/IEmailService.cs          (unchanged)     │
│  └── Options/EmailSettings.cs               (NEW)           │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ implements
┌─────────────────────────────────────────────────────────────┐
│  HAMBOX.Modules.Identity.Infrastructure                     │
│  ├── Services/SmtpEmailService.cs           (NEW)         │
│  ├── Services/LoggingEmailService.cs        (RENAME)       │
│  ├── Services/EmailMessageBuilder.cs          (NEW)         │
│  ├── Authentication/EmailSettingsValidator.cs (NEW)         │
│  └── Extensions/IdentityInfrastructureExtensions.cs (MOD)   │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ configures
┌─────────────────────────────────────────────────────────────┐
│  HAMBOX.API                                                 │
│  ├── appsettings.json                         (MOD)         │
│  └── appsettings.Development.json             (MOD)         │
└─────────────────────────────────────────────────────────────┘
```

**Rules:**

- `MailKit` package reference **only** in `HAMBOX.Modules.Identity.Infrastructure.csproj`
- Application layer has **zero** SMTP/MailKit dependencies
- Link URL construction lives in Infrastructure (`EmailMessageBuilder`)
- Handlers remain unaware of SMTP vs logging implementation

### 3.2 Implementation selection (factory via DI)

| `EmailSettings.Enabled` | Implementation | Use case |
|-------------------------|----------------|----------|
| `true` | `SmtpEmailService` | Docker Mailpit, staging, production SMTP |
| `false` | `LoggingEmailService` | Local dev without mail server; CI without SMTP |

Registration pattern (conceptual):

```csharp
services.AddScoped<SmtpEmailService>();
services.AddScoped<LoggingEmailService>();
services.AddScoped<IEmailService>(sp =>
    sp.GetRequiredService<IOptions<EmailSettings>>().Value.Enabled
        ? sp.GetRequiredService<SmtpEmailService>()
        : sp.GetRequiredService<LoggingEmailService>());
```

Both implementations are `internal sealed` — not exposed outside Infrastructure.

---

## 4. Configuration Model

### 4.1 `EmailSettings` (Application layer)

**File:** `src/Modules/Identity/HAMBOX.Modules.Identity.Application/Options/EmailSettings.cs`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SectionName` | `const string` | `"EmailSettings"` | Config section key |
| `Enabled` | `bool` | `true` | `false` → logging fallback |
| `SmtpHost` | `string` | `""` | SMTP server hostname |
| `SmtpPort` | `int` | `587` | SMTP port (1025 Mailpit, 587 TLS) |
| `UseSsl` | `bool` | `false` | STARTTLS / SSL (false for Mailpit) |
| `Username` | `string?` | `null` | SMTP auth user (optional) |
| `Password` | `string?` | `null` | SMTP auth password (env var only in prod) |
| `FromAddress` | `string` | `""` | Sender email (required when enabled) |
| `FromName` | `string` | `"HAMBOX"` | Sender display name |
| `ApplicationBaseUrl` | `string` | `""` | Public app URL for links in emails |
| `VerificationPath` | `string` | `"/auth/verify-email"` | Frontend route; query `?token=` appended |
| `ResetPasswordPath` | `string` | `"/auth/reset-password"` | Frontend route; query `?token=` appended |

**Link format (emails):**

```
{ApplicationBaseUrl}{VerificationPath}?token={url-encoded-token}
{ApplicationBaseUrl}{ResetPasswordPath}?token={url-encoded-token}
```

> **Note:** Verify/reset API endpoints are `POST`. Email links target the **future Angular app**, which will read `token` from the query string and call the API. Until the frontend exists, developers can copy the token from Mailpit and call Swagger/curl. README will document this.

### 4.2 `appsettings.json` (committed — no secrets)

```json
"EmailSettings": {
  "Enabled": true,
  "SmtpHost": "localhost",
  "SmtpPort": 1025,
  "UseSsl": false,
  "FromAddress": "noreply@hambox.local",
  "FromName": "HAMBOX",
  "ApplicationBaseUrl": "http://localhost:3000",
  "VerificationPath": "/auth/verify-email",
  "ResetPasswordPath": "/auth/reset-password"
}
```

### 4.3 `appsettings.Development.json` (local overrides)

- Same Mailpit defaults when running API outside Docker
- `Enabled: true` for integration testing with Mailpit

### 4.4 Environment variables (production / Docker)

| Variable | Maps to |
|----------|---------|
| `EmailSettings__Enabled` | `true` / `false` |
| `EmailSettings__SmtpHost` | `mailpit` (Docker network) |
| `EmailSettings__SmtpPort` | `1025` |
| `EmailSettings__UseSsl` | `false` |
| `EmailSettings__Username` | optional |
| `EmailSettings__Password` | **secret** — never commit |
| `EmailSettings__FromAddress` | `noreply@yourdomain.com` |
| `EmailSettings__ApplicationBaseUrl` | `https://app.hambox.com` |

### 4.5 Startup validation — `EmailSettingsValidator`

**File:** `src/Modules/Identity/HAMBOX.Modules.Identity.Infrastructure/Authentication/EmailSettingsValidator.cs`

Follows `JwtSettingsValidator` pattern (`IValidateOptions<EmailSettings>`).

| Condition | Validation |
|-----------|------------|
| `Enabled == false` | Success (no SMTP required) |
| `Enabled == true` | `SmtpHost` required |
| `Enabled == true` | `SmtpPort` in range 1–65535 |
| `Enabled == true` | `FromAddress` required and valid email format |
| `Enabled == true` | `ApplicationBaseUrl` required, absolute URI |
| `Enabled == true` + `UseSsl == true` | Warn in log if port is 25 (informational only) |

**Fail fast at startup** (same as JWT) when `Enabled=true` and config is invalid.

---

## 5. Service Implementations

### 5.1 `SmtpEmailService` (NEW)

**File:** `src/Modules/Identity/HAMBOX.Modules.Identity.Infrastructure/Services/SmtpEmailService.cs`

**Dependencies:** `IOptions<EmailSettings>`, `ILogger<SmtpEmailService>`, `EmailMessageBuilder`

**Per-send flow:**

1. Validate `email` and `token` are non-empty (`ArgumentException` if invalid — developer error)
2. Build `MimeMessage` via `EmailMessageBuilder` (HTML + `text/plain` alternative)
3. Connect with MailKit `SmtpClient`:
   - `ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable | None based on UseSsl)`
   - Authenticate only if `Username` is not null/empty
   - `SendAsync(message)`
   - `DisconnectAsync(true)`
4. Structured log on success
5. On `SmtpCommandException` / `SmtpProtocolException` / `IOException`: structured **Error** log with correlation ID — **do not throw** (client already received success from handler after persistence).

**Timeouts:** `client.Timeout = 30_000` ms.

**Cancellation:** Pass `cancellationToken` to all MailKit async methods.

### 5.2 `LoggingEmailService` (RENAME from `EmailService`)

**File:** rename `EmailService.cs` → `LoggingEmailService.cs`

**Behavior when `Enabled=false`:**

- Log at **Information**: message type, `UserId`, masked email (`a***@domain.com`), `ExpiresAt`
- Log at **Debug**: full verification/reset URL (dev convenience — **not** in Production default log level)
- Return completed task (no throw)

Preserves today’s non-blocking dev experience when SMTP is intentionally disabled.

### 5.3 `EmailMessageBuilder` (NEW)

**File:** `src/Modules/Identity/HAMBOX.Modules.Identity.Infrastructure/Services/EmailMessageBuilder.cs`

Static or small injectable helper — builds `MimeMessage` bodies:

| Template | Subject | Body contains |
|----------|---------|---------------|
| Verification | `Verify your HAMBOX account` | Greeting, CTA link, expiry time, plain-text fallback |
| Password reset | `Reset your HAMBOX password` | Same pattern |

- HTML: minimal inline styles (email-client safe)
- Plain text: URL on its own line
- `expiresAt` formatted as UTC ISO 8601 in body
- No user PII beyond email address and first name (not available in `IEmailService` today — use generic greeting)

### 5.4 Email failure handling (no exception to client)

`SmtpEmailService` **never throws** on SMTP delivery failure. Handlers always return success after persistence. Correlation ID read from `HttpContext.Items["CorrelationId"]` via `IHttpContextAccessor`.

**Post-save failure behavior (APPROVED):**

- Persist user/token first, then attempt email delivery.
- If SMTP fails: log exception with **correlation ID**, do **not** roll back, do **not** throw.
- Handlers return **success** to the client.
- Recovery via **`POST /api/auth/resend-verification`** (included in this implementation).

---

## 5.5 Resend Verification Email (APPROVED)

**Endpoint:** `POST /api/auth/resend-verification`

**Request body:** `{ "email": "user@example.com" }`

**Handler behavior:**

1. Normalize email and look up user.
2. If user does not exist → return success (anti-enumeration).
3. If `EmailConfirmed` → return success.
4. Remove prior unused verification tokens for the user.
5. Issue new `EmailVerificationToken` (24h expiry).
6. `SaveChangesAsync`, then attempt email (failures logged, not thrown).
7. Always return success.

**Files:** `ResendVerificationCommand`, `ResendVerificationCommandHandler`, `ResendVerificationCommandValidator`, `AuthEndpoints.cs`.

---

## 6. Structured Logging Specification

Use `ILogger` with named properties (Serilog will capture as structured fields):

| Event | Level | Properties |
|-------|-------|------------|
| Send started | Debug | `EmailType`, `UserId`, `RecipientDomain` (not full email in prod) |
| Send succeeded | Information | `EmailType`, `UserId`, `ElapsedMs` |
| Send failed | Error | `EmailType`, `UserId`, `SmtpHost`, `SmtpPort`, `Exception` |
| Fallback logging impl | Information | `EmailType`, `UserId`, `Mode=LoggingFallback` |

**Email masking helper:** `MaskEmail(string email)` → `r***@example.com` for Information logs; full address at Debug only.

---

## 7. Docker / Mailpit Integration

### 7.1 `docker-compose.yml` changes

Add service:

```yaml
mailpit:
  image: axllent/mailpit:latest
  container_name: mailpit
  ports:
    - "8025:8025"   # Web UI — http://localhost:8025
    - "1025:1025"   # SMTP
  restart: unless-stopped
```

Update `hambox-api`:

```yaml
depends_on:
  sqlserver:
    condition: service_healthy
  mailpit:
    condition: service_started
environment:
  - EmailSettings__Enabled=true
  - EmailSettings__SmtpHost=mailpit
  - EmailSettings__SmtpPort=1025
  - EmailSettings__UseSsl=false
  - EmailSettings__FromAddress=noreply@hambox.local
  - EmailSettings__FromName=HAMBOX
  - EmailSettings__ApplicationBaseUrl=http://localhost:3000
```

### 7.2 `.env` additions (documented in README)

```env
EMAIL_SMTP_HOST=mailpit
EMAIL_SMTP_PORT=1025
```

(Map via compose `environment` block — no secrets needed for Mailpit.)

### 7.3 Local dev without Docker

1. Run Mailpit standalone: `docker run -d -p 8025:8025 -p 1025:1025 axllent/mailpit`
2. API uses `appsettings.Development.json` → `localhost:1025`

---

## 8. Package Changes

**`Directory.Packages.props`:**

```xml
<PackageVersion Include="MailKit" Version="4.13.0" />
```

**`HAMBOX.Modules.Identity.Infrastructure.csproj`:**

```xml
<PackageReference Include="MailKit" />
```

(MailKit 4.x targets .NET 8+; compatible with .NET 10.)

---

## 9. Files to Create / Modify

### Create

| File | Layer |
|------|-------|
| `Options/EmailSettings.cs` | Application |
| `Services/SmtpEmailService.cs` | Infrastructure |
| `Services/LoggingEmailService.cs` | Infrastructure (rename from EmailService) |
| `Services/EmailMessageBuilder.cs` | Infrastructure |
| `Features/ResendVerification/*` | Application |
| `Authentication/EmailSettingsValidator.cs` | Infrastructure |

### Modify

| File | Change |
|------|--------|
| `IdentityInfrastructureExtensions.cs` | Configure `EmailSettings`, validator, conditional `IEmailService` DI |
| `HAMBOX.Modules.Identity.Infrastructure.csproj` | Add MailKit |
| `Directory.Packages.props` | Add MailKit version |
| `appsettings.json` | Add `EmailSettings` section |
| `appsettings.Development.json` | Mailpit defaults |
| `docker-compose.yml` | Add Mailpit + API env vars |
| `README.md` | Email configuration, Mailpit UI URL, troubleshooting |

### Delete

| File | Reason |
|------|--------|
| `Services/EmailService.cs` | Replaced by `LoggingEmailService.cs` |

### Unchanged

| File | Reason |
|------|--------|
| `IEmailService.cs` | Contract preserved |
| `RegisterCommandHandler.cs` | No changes |
| `ForgotPasswordCommandHandler.cs` | No changes |
| `AuthEndpoints.cs` | No changes |

---

## 10. Implementation Steps (Ordered)

| Step | Task | Effort |
|------|------|--------|
| 1 | Add MailKit to central packages + Infrastructure csproj | 15 min |
| 2 | Create `EmailSettings` in Application/Options | 30 min |
| 3 | Create `EmailSettingsValidator` | 45 min |
| 4 | Create `EmailMessageBuilder` (verification + reset templates) | 1.5 h |
| 5 | Implement `SmtpEmailService` with MailKit + structured logging | 2 h |
| 6 | Rename/refactor `LoggingEmailService` with masked logging | 45 min |
| 7 | Wire DI + startup validation in `IdentityInfrastructureExtensions` | 45 min |
| 8 | Update `appsettings.json` + `appsettings.Development.json` | 30 min |
| 9 | Add Mailpit to `docker-compose.yml` + API env vars | 45 min |
| 10 | Update `README.md` | 30 min |
| 11 | Manual verification (see §11) | 1 h |

**Total estimate: 1–1.5 days**

---

## 11. Acceptance Criteria

### Functional

- [ ] Register → email appears in Mailpit UI with verification link containing token
- [ ] Forgot-password → email appears in Mailpit with reset link containing token
- [ ] `EmailSettings:Enabled=false` → no SMTP connection; informational log only; handlers still succeed
- [ ] Invalid SMTP config with `Enabled=true` → application **fails to start** with clear validation message
- [ ] `IEmailService` interface unchanged; handlers unchanged

### Non-functional

- [ ] No SMTP passwords in committed config files
- [ ] MailKit referenced only from Infrastructure project
- [ ] Structured logs include `EmailType`, `UserId`; no credentials in logs
- [ ] SMTP failure produces 500 to client (after user saved) with generic ProblemDetails message
- [ ] Docker Compose `up` brings Mailpit + API; emails capturable at `http://localhost:8025`

### Manual test script (post-implementation)

```http
POST /api/auth/register
{ "email": "test@example.com", "password": "...", "firstName": "Test", "lastName": "User" }

# Check Mailpit → copy token from email link
POST /api/auth/verify-email?token={token}

POST /api/auth/forgot-password
{ "email": "test@example.com" }

# Check Mailpit → copy reset token
POST /api/auth/reset-password
{ "token": "...", "newPassword": "..." }
```

---

## 12. Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Email fails after DB commit | Document; future: outbox/resend endpoint (Sprint 2+) |
| Frontend routes don't exist yet | Links point to `ApplicationBaseUrl` paths; README explains Mailpit + Swagger workflow |
| Production SMTP blocked | Configurable `Enabled=false` fallback; env-based provider swap |
| HTML emails marked spam | Production: SPF/DKIM on sender domain (ops, not code) |
| Secrets in appsettings | Validator + README; password via env var only |

---

## 13. Out of Scope (This PR)

- Changing `IEmailService` to return `Result` (would require handler changes)
- Email outbox / retry queue (Redis — later sprint)
- SendGrid / AWS SES SDK (SMTP is provider-agnostic)
- Localized email templates (AR/EN — with localization epic)
- Integration tests with `FakeEmailService` (separate P0 backlog item)
- Resend verification / resend reset endpoints
- GET-based verify link API endpoint (frontend handles POST)

---

## 14. Approval Checklist

Please confirm before implementation:

- [ ] **A.** `EmailSettings` location in Application/Options is acceptable
- [ ] **B.** Link URLs target frontend routes (`/auth/verify-email`, `/auth/reset-password`) — not direct API POST links
- [ ] **C.** `Enabled=false` logging fallback behavior is acceptable for CI/local
- [x] **D.** SMTP send failure logs only; client receives success; resend endpoint provided
- [ ] **E.** Mailpit as Docker dev mail server is acceptable
- [ ] **F.** MailKit 4.13.x package choice is acceptable

---

**Status:** Approved — implementation in progress.
