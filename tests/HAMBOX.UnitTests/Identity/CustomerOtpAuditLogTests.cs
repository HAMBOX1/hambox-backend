using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Features.ForgotPassword;
using HAMBOX.Modules.Identity.Application.Features.ResendVerification;
using HAMBOX.Modules.Identity.Application.Features.ResetPassword;
using HAMBOX.Modules.Identity.Application.Features.VerifyEmail;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Coverage for the customer OTP/verification-token audit trail (email verification, password
/// reset, resend) added on top of the existing token-hashing flows already proven by
/// <see cref="AccountTokenHashingTests"/>. Proves: an audit row is written for every lifecycle
/// event, it never carries the plaintext token, the recorded purpose/status/expiration/user are
/// correct, email delivery outcome is captured (including failure), and a resend invalidates the
/// superseded token's audit trail rather than deleting it silently.
/// </summary>
public sealed class CustomerOtpAuditLogTests
{
    private const string Plaintext = "a-cryptographically-random-looking-token-value-123";

    private static IdentityDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new IdentityDbContext(options);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";
        public bool VerifyPassword(string hashedPassword, string providedPassword) => hashedPassword == $"hashed:{providedPassword}";
    }

    private sealed class FakeSecurityEventLogger : ISecurityEventLogger
    {
        public int CallCount { get; private set; }

        public Task LogAsync(
            SecurityEventType eventType,
            SecurityEventSeverity severity,
            string description,
            Guid? actorUserId = null,
            Guid? targetUserId = null,
            string? ipAddress = null,
            string? country = null,
            string? userAgent = null,
            string? correlationId = null,
            string? city = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        private readonly Queue<string> _values;
        public FakeTokenGenerator(params string[] values) => _values = new Queue<string>(values);
        public string GenerateSecureToken() => _values.Count > 0 ? _values.Dequeue() : Guid.NewGuid().ToString("N");
    }

    /// <summary>Records every call made to it and lets the test control whether "sending" succeeds —
    /// exactly the signal <see cref="IEmailService"/>'s real implementations now surface, proving
    /// handlers react to it instead of assuming success.</summary>
    private sealed class FakeEmailService(bool deliverySucceeds = true) : IEmailService
    {
        public List<(string Kind, Guid UserId, string Email, string? Secret)> Calls { get; } = [];

        public Task<bool> SendEmailVerificationAsync(Guid userId, string email, DateTimeOffset expiresAt, string token, CancellationToken cancellationToken = default)
        {
            Calls.Add(("EmailVerification", userId, email, token));
            return Task.FromResult(deliverySucceeds);
        }

        public Task<bool> SendPasswordResetAsync(Guid userId, string email, DateTimeOffset expiresAt, string token, CancellationToken cancellationToken = default)
        {
            Calls.Add(("PasswordReset", userId, email, token));
            return Task.FromResult(deliverySucceeds);
        }

        public Task<bool> SendAdminLoginOtpAsync(Guid userId, string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
        {
            Calls.Add(("AdminLoginOtp", userId, email, code));
            return Task.FromResult(deliverySucceeds);
        }

        public Task SendTemplatedEmailAsync(string toEmail, string subject, string htmlBody, string? correlationId = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static ApplicationUser SeedUser(IdentityDbContext db)
    {
        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);
        return user;
    }

    // --- ForgotPassword: issuance -----------------------------------------------------------------

    [Fact]
    public async Task ForgotPassword_IssuesToken_RecordsPendingAuditRow_WithCorrectPurposeUserAndExpiry()
    {
        await using var db = CreateDb();
        var user = SeedUser(db);
        await db.SaveChangesAsync();

        var emailService = new FakeEmailService();
        var handler = new ForgotPasswordCommandHandler(db, new FakeTokenGenerator(Plaintext), emailService);

        var result = await handler.Handle(
            new ForgotPasswordCommand(user.Email, "203.0.113.1", "turnstile-token", "TestAgent/1.0", "corr-1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var auditRow = Assert.Single(db.CustomerOtpAuditLogs.AsNoTracking());
        Assert.Equal(CustomerOtpPurpose.PasswordReset, auditRow.Purpose);
        Assert.Equal(CustomerOtpEventStatus.Pending, auditRow.Status);
        Assert.Equal(user.Id, auditRow.UserId);
        Assert.Equal("203.0.113.1", auditRow.IpAddress);
        Assert.Equal("TestAgent/1.0", auditRow.UserAgent);
        Assert.Equal("corr-1", auditRow.CorrelationId);
        Assert.Equal(CustomerOtpEmailDeliveryStatus.Sent, auditRow.EmailDeliveryStatus);

        var resetToken = Assert.Single(db.PasswordResetTokens.AsNoTracking());
        Assert.Equal(resetToken.Id, auditRow.TokenId);
        Assert.Equal(resetToken.ExpiresOnUtc, auditRow.ExpiresOnUtc);

        // The email service really was called with the plaintext (that's how the user gets it) —
        // but the audit row itself must never carry it anywhere.
        var call = Assert.Single(emailService.Calls);
        Assert.Equal(Plaintext, call.Secret);
        AssertNoPropertyContainsPlaintext(auditRow, Plaintext);
    }

    [Fact]
    public async Task ForgotPassword_EmailDeliveryFails_AuditRowRecordsFailed_ButRequestStillSucceeds()
    {
        await using var db = CreateDb();
        var user = SeedUser(db);
        await db.SaveChangesAsync();

        var handler = new ForgotPasswordCommandHandler(db, new FakeTokenGenerator(Plaintext), new FakeEmailService(deliverySucceeds: false));

        var result = await handler.Handle(
            new ForgotPasswordCommand(user.Email, "203.0.113.1", "turnstile-token"), CancellationToken.None);

        // A dead mail server must not turn into "your password reset request failed" — the token is
        // still valid and usable if the user gets the link some other way; only delivery is flagged.
        Assert.True(result.IsSuccess);

        var auditRow = Assert.Single(db.CustomerOtpAuditLogs.AsNoTracking());
        Assert.Equal(CustomerOtpEmailDeliveryStatus.Failed, auditRow.EmailDeliveryStatus);
        Assert.Equal(CustomerOtpEventStatus.Pending, auditRow.Status);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_NoAuditRowWritten_NoEnumerationLeak()
    {
        await using var db = CreateDb();

        var handler = new ForgotPasswordCommandHandler(db, new FakeTokenGenerator(Plaintext), new FakeEmailService());
        var result = await handler.Handle(
            new ForgotPasswordCommand("nobody@example.com", "203.0.113.1", "turnstile-token"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(db.CustomerOtpAuditLogs);
    }

    // --- ResetPassword: use / failure / expiry ------------------------------------------------------

    [Fact]
    public async Task ResetPassword_ValidToken_RecordsUsedAuditRow()
    {
        await using var db = CreateDb();
        var user = SeedUser(db);
        var resetToken = PasswordResetToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(1));
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(db, new FakePasswordHasher(), new FakeSecurityEventLogger());
        var result = await handler.Handle(
            new ResetPasswordCommand(Plaintext, "NewPassword123!", "203.0.113.1", "TestAgent/1.0", "corr-2"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var auditRow = Assert.Single(db.CustomerOtpAuditLogs.AsNoTracking());
        Assert.Equal(CustomerOtpPurpose.PasswordReset, auditRow.Purpose);
        Assert.Equal(CustomerOtpEventStatus.Used, auditRow.Status);
        Assert.Equal(user.Id, auditRow.UserId);
        Assert.Equal(resetToken.Id, auditRow.TokenId);
        Assert.NotNull(auditRow.UsedOnUtc);
        AssertNoPropertyContainsPlaintext(auditRow, Plaintext);
    }

    [Fact]
    public async Task ResetPassword_WrongToken_RecordsFailedAuditRow_UnattributedToAnyUser_AndRaisesSecurityEvent()
    {
        await using var db = CreateDb();
        var user = SeedUser(db);
        db.PasswordResetTokens.Add(PasswordResetToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var securityLogger = new FakeSecurityEventLogger();
        var handler = new ResetPasswordCommandHandler(db, new FakePasswordHasher(), securityLogger);
        var result = await handler.Handle(
            new ResetPasswordCommand("not-the-right-token", "NewPassword123!"), CancellationToken.None);

        Assert.False(result.IsSuccess);

        var auditRow = Assert.Single(db.CustomerOtpAuditLogs.AsNoTracking());
        Assert.Equal(CustomerOtpEventStatus.Failed, auditRow.Status);
        Assert.Null(auditRow.UserId); // a guessed value can't be attributed to anyone
        Assert.Null(auditRow.TokenId);
        Assert.Equal(1, securityLogger.CallCount);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_RecordsExpiredAuditRow_AttributedToTheRightUser()
    {
        await using var db = CreateDb();
        var user = SeedUser(db);
        var resetToken = PasswordResetToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddMinutes(1));
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();
        db.Entry(resetToken).Property(nameof(PasswordResetToken.ExpiresOnUtc)).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(db, new FakePasswordHasher(), new FakeSecurityEventLogger());
        var result = await handler.Handle(new ResetPasswordCommand(Plaintext, "NewPassword123!"), CancellationToken.None);

        Assert.False(result.IsSuccess);

        var auditRow = Assert.Single(db.CustomerOtpAuditLogs.AsNoTracking());
        Assert.Equal(CustomerOtpEventStatus.Expired, auditRow.Status);
        Assert.Equal(user.Id, auditRow.UserId);
        Assert.Equal(resetToken.Id, auditRow.TokenId);
    }

    // --- VerifyEmail: use / failure / expiry ----------------------------------------------------

    private static async Task<HAMBOX.Modules.Identity.Domain.Roles.ApplicationRole> SeedDefaultRoleAsync(IdentityDbContext db)
    {
        var role = HAMBOX.Modules.Identity.Domain.Roles.ApplicationRole.Create("Customer", isDefault: true);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    [Fact]
    public async Task VerifyEmail_ValidToken_RecordsUsedAuditRow()
    {
        await using var db = CreateDb();
        await SeedDefaultRoleAsync(db);
        var user = SeedUser(db);
        var verificationToken = EmailVerificationToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(24));
        db.EmailVerificationTokens.Add(verificationToken);
        await db.SaveChangesAsync();

        var handler = new VerifyEmailCommandHandler(db, new FakeSecurityEventLogger());
        var result = await handler.Handle(
            new VerifyEmailCommand(Plaintext, "203.0.113.1", "TestAgent/1.0", "corr-3"), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var auditRow = Assert.Single(db.CustomerOtpAuditLogs.AsNoTracking());
        Assert.Equal(CustomerOtpPurpose.EmailVerification, auditRow.Purpose);
        Assert.Equal(CustomerOtpEventStatus.Used, auditRow.Status);
        Assert.Equal(user.Id, auditRow.UserId);
        Assert.Equal(verificationToken.Id, auditRow.TokenId);
        Assert.NotNull(auditRow.UsedOnUtc);
        AssertNoPropertyContainsPlaintext(auditRow, Plaintext);
    }

    [Fact]
    public async Task VerifyEmail_UnknownToken_RecordsFailedAuditRow_UnattributedToAnyUser()
    {
        await using var db = CreateDb();
        await SeedDefaultRoleAsync(db);

        var handler = new VerifyEmailCommandHandler(db, new FakeSecurityEventLogger());
        var result = await handler.Handle(new VerifyEmailCommand("garbage-guessed-value"), CancellationToken.None);

        Assert.False(result.IsSuccess);

        var auditRow = Assert.Single(db.CustomerOtpAuditLogs.AsNoTracking());
        Assert.Equal(CustomerOtpEventStatus.Failed, auditRow.Status);
        Assert.Null(auditRow.UserId);
        Assert.Null(auditRow.IssuedOnUtc);
        Assert.Null(auditRow.ExpiresOnUtc);
    }

    [Fact]
    public async Task VerifyEmail_ExpiredToken_RecordsExpiredAuditRow()
    {
        await using var db = CreateDb();
        await SeedDefaultRoleAsync(db);
        var user = SeedUser(db);
        var verificationToken = EmailVerificationToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddMinutes(1));
        db.EmailVerificationTokens.Add(verificationToken);
        await db.SaveChangesAsync();
        db.Entry(verificationToken).Property(nameof(EmailVerificationToken.ExpiresOnUtc)).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var handler = new VerifyEmailCommandHandler(db, new FakeSecurityEventLogger());
        var result = await handler.Handle(new VerifyEmailCommand(Plaintext), CancellationToken.None);

        Assert.False(result.IsSuccess);

        var auditRow = Assert.Single(db.CustomerOtpAuditLogs.AsNoTracking());
        Assert.Equal(CustomerOtpEventStatus.Expired, auditRow.Status);
        Assert.Equal(user.Id, auditRow.UserId);
    }

    // --- ResendVerification: invalidation + reissue -------------------------------------------------

    [Fact]
    public async Task ResendVerification_SupersedesOldToken_InvalidatesItsAuditTrail_AndIssuesANewPendingRow()
    {
        await using var db = CreateDb();
        var user = SeedUser(db);
        const string oldPlaintext = "old-verification-token-value";
        var oldToken = EmailVerificationToken.Create(user.Id, oldPlaintext, DateTimeOffset.UtcNow.AddHours(24));
        db.EmailVerificationTokens.Add(oldToken);
        await db.SaveChangesAsync();

        const string newPlaintext = "new-verification-token-value";
        var emailService = new FakeEmailService();
        var handler = new ResendVerificationCommandHandler(db, new FakeTokenGenerator(newPlaintext), emailService);

        var result = await handler.Handle(
            new ResendVerificationCommand(user.Email, "203.0.113.1", "turnstile-token", "TestAgent/1.0", "corr-4"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        // The old token row is hard-deleted...
        Assert.Empty(db.EmailVerificationTokens.AsNoTracking().Where(t => t.Id == oldToken.Id));

        // ...but its audit trail survives as an Invalidated event, and a fresh Pending event exists
        // for the newly-issued token — two rows, not one overwritten row.
        var rows = db.CustomerOtpAuditLogs.AsNoTracking().OrderBy(r => r.Status).ToList();
        Assert.Equal(2, rows.Count);

        var invalidated = Assert.Single(rows, r => r.Status == CustomerOtpEventStatus.Invalidated);
        Assert.Equal(oldToken.Id, invalidated.TokenId);
        Assert.Equal(user.Id, invalidated.UserId);

        var pending = Assert.Single(rows, r => r.Status == CustomerOtpEventStatus.Pending);
        Assert.Equal(user.Id, pending.UserId);
        Assert.NotEqual(oldToken.Id, pending.TokenId);

        var newToken = Assert.Single(db.EmailVerificationTokens.AsNoTracking());
        Assert.Equal(newToken.Id, pending.TokenId);

        var call = Assert.Single(emailService.Calls);
        Assert.Equal(newPlaintext, call.Secret);
    }

    // --- Reflection guard: no field on the audit entity can ever hold the plaintext ----------------

    private static void AssertNoPropertyContainsPlaintext(object entity, string plaintext)
    {
        foreach (var property in entity.GetType().GetProperties())
        {
            if (property.PropertyType != typeof(string))
            {
                continue;
            }

            var value = property.GetValue(entity) as string;
            Assert.False(
                value is not null && value.Contains(plaintext, StringComparison.Ordinal),
                $"Property '{property.Name}' unexpectedly contained the plaintext token value.");
        }
    }
}
