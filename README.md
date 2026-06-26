# HAMBOX Web API

Modular monolith API for the HAMBOX platform.

## Configuration

### JWT secret (required)

The application **will not start** without a JWT signing key. Do not commit secrets to source control.

| Environment | How to provide `JwtSettings:SecretKey` |
|-------------|----------------------------------------|
| Local development | `src/API/HAMBOX.API/appsettings.Development.json` or User Secrets |
| Docker Compose | `JWT_SECRET_KEY` in `.env` (mapped to `JwtSettings__SecretKey`) |
| Production | Environment variable `JwtSettings__SecretKey` or your secret store |

Minimum key length: **32 characters**.

### Docker Compose

1. Copy or update `.env` in the repository root.
2. Set `JWT_SECRET_KEY` to a strong random value (at least 32 characters).
3. Run `docker compose up --build`.

Example `.env` entries:

```env
SA_PASSWORD=YourStrongSqlPassword!
DB_NAME=HamboxDb
ASPNETCORE_ENVIRONMENT=Development
JWT_SECRET_KEY=YourStrongJwtSigningKeyAtLeast32CharsLong!
```

### Lockout policy

Configured under `LockoutSettings` in `appsettings.json`:

- `MaxFailedAccessAttempts` (default: 5)
- `LockoutDurationMinutes` (default: 15)

### Email delivery

Transactional emails (verification, password reset) use SMTP via MailKit when `EmailSettings:Enabled` is `true`. When `false`, messages are logged only.

| Environment | Configuration |
|-------------|---------------|
| Local + Docker Compose | Mailpit on port `1025`; web UI at [http://localhost:8025](http://localhost:8025) |
| Local without Docker | Run `docker run -d -p 8025:8025 -p 1025:1025 axllent/mailpit` and use `localhost:1025` |
| Production | Set `EmailSettings__SmtpHost`, `EmailSettings__Password`, etc. via environment variables |

**Important:** If SMTP delivery fails after the database transaction commits, the API still returns success. Failures are logged with the request correlation ID. Users can recover using `POST /api/auth/resend-verification`.

Example `EmailSettings` in `appsettings.json`:

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

Do not commit SMTP passwords. Use `EmailSettings__Password` in production.

### Development admin account

In **Development** only, the API seeds a verified admin user for catalog CRUD testing (see `DevAdminSeed` in `appsettings.Development.json`):

| Field | Default |
|-------|---------|
| Email | `admin@hambox.local` |
| Password | `Admin123!` |
| Role | `Admin` (products + categories CRUD) |

Log in via `POST /api/auth/login`, then use the returned JWT on protected catalog endpoints. Set `DevAdminSeed:Enabled` to `false` to disable seeding.
