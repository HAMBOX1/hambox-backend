using System.Security.Cryptography;
using System.Text;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.ResetPassword;
using HAMBOX.Modules.Identity.Application.Features.VerifyEmail;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Roles;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Proves <see cref="PasswordResetToken"/> and <see cref="EmailVerificationToken"/> store only a
/// SHA-256 lookup hash of the plaintext token — mirroring <see cref="HAMBOX.Modules.Identity.Domain.Tokens.RefreshToken"/>'s
/// already-correct pattern — never the plaintext value itself, and that both the password-reset and
/// email-verification flows still work end-to-end for a caller holding the plaintext token from the
/// email, while still correctly rejecting wrong, expired, or already-used tokens.
/// </summary>
public sealed class AccountTokenHashingTests
{
    private const string Plaintext = "a-cryptographically-random-looking-token-value-123";

    private static string IndependentSha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static IdentityDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new IdentityDbContext(options);
    }

    private sealed class FakePasswordHasher : HAMBOX.Modules.Identity.Application.Abstractions.IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";
        public bool VerifyPassword(string hashedPassword, string providedPassword) => hashedPassword == $"hashed:{providedPassword}";
    }

    /// <summary>No-op stand-in for the real DB/email-backed <see cref="ISecurityEventLogger"/> — these
    /// tests only need the handlers to compile and run, not to assert on Security Center side effects.</summary>
    private sealed class FakeSecurityEventLogger : ISecurityEventLogger
    {
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
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // --- PasswordResetToken: storage & hash-matching in isolation ---------------------------------

    [Fact]
    public void PasswordResetToken_Create_NeverStoresThePlaintextValue()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), Plaintext, DateTimeOffset.UtcNow.AddHours(1));

        Assert.NotEqual(Plaintext, token.Token);
        Assert.Equal(IndependentSha256Hex(Plaintext), token.Token);
    }

    [Fact]
    public void PasswordResetToken_GetLookupHash_MatchesWhatCreateStored()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), Plaintext, DateTimeOffset.UtcNow.AddHours(1));

        Assert.Equal(token.Token, PasswordResetToken.GetLookupHash(Plaintext));
        Assert.NotEqual(token.Token, PasswordResetToken.GetLookupHash("a-different-token-value"));
    }

    [Fact]
    public void PasswordResetToken_Matches_TrueForCorrectPlaintext_FalseForWrongPlaintext()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), Plaintext, DateTimeOffset.UtcNow.AddHours(1));

        Assert.True(token.Matches(Plaintext));
        Assert.False(token.Matches("wrong-value"));
    }

    // --- EmailVerificationToken: storage & hash-matching in isolation -----------------------------

    [Fact]
    public void EmailVerificationToken_Create_NeverStoresThePlaintextValue()
    {
        var token = EmailVerificationToken.Create(Guid.NewGuid(), Plaintext, DateTimeOffset.UtcNow.AddHours(24));

        Assert.NotEqual(Plaintext, token.Token);
        Assert.Equal(IndependentSha256Hex(Plaintext), token.Token);
    }

    [Fact]
    public void EmailVerificationToken_GetLookupHash_MatchesWhatCreateStored()
    {
        var token = EmailVerificationToken.Create(Guid.NewGuid(), Plaintext, DateTimeOffset.UtcNow.AddHours(24));

        Assert.Equal(token.Token, EmailVerificationToken.GetLookupHash(Plaintext));
        Assert.NotEqual(token.Token, EmailVerificationToken.GetLookupHash("a-different-token-value"));
    }

    [Fact]
    public void EmailVerificationToken_Matches_TrueForCorrectPlaintext_FalseForWrongPlaintext()
    {
        var token = EmailVerificationToken.Create(Guid.NewGuid(), Plaintext, DateTimeOffset.UtcNow.AddHours(24));

        Assert.True(token.Matches(Plaintext));
        Assert.False(token.Matches("wrong-value"));
    }

    // --- ResetPasswordCommandHandler: end-to-end through a real (InMemory) IdentityDbContext ------

    [Fact]
    public async Task ResetPasswordCommandHandler_PlaintextNeverPersisted_ButValidTokenStillWorks()
    {
        await using var db = CreateDb();

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);

        var resetToken = PasswordResetToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(1));
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        // Prove it directly against what's actually stored, not the in-memory entity reference.
        var persisted = await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.Id == resetToken.Id);
        Assert.NotEqual(Plaintext, persisted.Token);
        Assert.DoesNotContain(Plaintext, persisted.Token, StringComparison.Ordinal);

        var handler = new ResetPasswordCommandHandler(db, new FakePasswordHasher(), new FakeSecurityEventLogger());
        var result = await handler.Handle(new ResetPasswordCommand(Plaintext, "NewPassword123!"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var reloadedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal("hashed:NewPassword123!", reloadedUser.PasswordHash);
    }

    [Fact]
    public async Task ResetPasswordCommandHandler_WrongToken_FailsWithInvalidToken()
    {
        await using var db = CreateDb();

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);
        db.PasswordResetTokens.Add(PasswordResetToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(db, new FakePasswordHasher(), new FakeSecurityEventLogger());
        var result = await handler.Handle(
            new ResetPasswordCommand("this-is-not-the-right-token", "NewPassword123!"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidToken.Code, result.Error.Code);
    }

    [Fact]
    public async Task ResetPasswordCommandHandler_ExpiredToken_FailsWithTokenExpired()
    {
        await using var db = CreateDb();

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);
        // Create() rejects an already-past expiry, so back-date it via the change tracker after
        // creation — the only way to get a genuinely expired row into the test database.
        var resetToken = PasswordResetToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddMinutes(1));
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();
        db.Entry(resetToken).Property(nameof(PasswordResetToken.ExpiresOnUtc)).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(db, new FakePasswordHasher(), new FakeSecurityEventLogger());
        var result = await handler.Handle(new ResetPasswordCommand(Plaintext, "NewPassword123!"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.TokenExpired.Code, result.Error.Code);
    }

    [Fact]
    public async Task ResetPasswordCommandHandler_AlreadyUsedToken_FailsWithInvalidToken()
    {
        await using var db = CreateDb();

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);
        var resetToken = PasswordResetToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(1));
        resetToken.MarkAsUsed();
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(db, new FakePasswordHasher(), new FakeSecurityEventLogger());
        var result = await handler.Handle(new ResetPasswordCommand(Plaintext, "NewPassword123!"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidToken.Code, result.Error.Code);
    }

    // --- VerifyEmailCommandHandler: end-to-end through a real (InMemory) IdentityDbContext ---------

    private static async Task<ApplicationRole> SeedDefaultRoleAsync(IdentityDbContext db)
    {
        var role = ApplicationRole.Create("Customer", isDefault: true);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    [Fact]
    public async Task VerifyEmailCommandHandler_PlaintextNeverPersisted_ButValidTokenStillWorks()
    {
        await using var db = CreateDb();
        await SeedDefaultRoleAsync(db);

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);

        var verificationToken = EmailVerificationToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(24));
        db.EmailVerificationTokens.Add(verificationToken);
        await db.SaveChangesAsync();

        var persisted = await db.EmailVerificationTokens.AsNoTracking().SingleAsync(t => t.Id == verificationToken.Id);
        Assert.NotEqual(Plaintext, persisted.Token);
        Assert.DoesNotContain(Plaintext, persisted.Token, StringComparison.Ordinal);

        var handler = new VerifyEmailCommandHandler(db, new FakeSecurityEventLogger());
        var result = await handler.Handle(new VerifyEmailCommand(Plaintext), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var reloadedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.True(reloadedUser.EmailConfirmed);
    }

    [Fact]
    public async Task VerifyEmailCommandHandler_WrongToken_FailsWithInvalidToken()
    {
        await using var db = CreateDb();
        await SeedDefaultRoleAsync(db);

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);
        db.EmailVerificationTokens.Add(EmailVerificationToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(24)));
        await db.SaveChangesAsync();

        var handler = new VerifyEmailCommandHandler(db, new FakeSecurityEventLogger());
        var result = await handler.Handle(new VerifyEmailCommand("this-is-not-the-right-token"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidToken.Code, result.Error.Code);

        var reloadedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.False(reloadedUser.EmailConfirmed);
    }

    [Fact]
    public async Task VerifyEmailCommandHandler_ExpiredToken_FailsWithTokenExpired()
    {
        await using var db = CreateDb();
        await SeedDefaultRoleAsync(db);

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);
        var verificationToken = EmailVerificationToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddMinutes(1));
        db.EmailVerificationTokens.Add(verificationToken);
        await db.SaveChangesAsync();
        db.Entry(verificationToken).Property(nameof(EmailVerificationToken.ExpiresOnUtc)).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var handler = new VerifyEmailCommandHandler(db, new FakeSecurityEventLogger());
        var result = await handler.Handle(new VerifyEmailCommand(Plaintext), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.TokenExpired.Code, result.Error.Code);
    }

    [Fact]
    public async Task VerifyEmailCommandHandler_AlreadyUsedToken_FailsWithInvalidToken()
    {
        await using var db = CreateDb();
        await SeedDefaultRoleAsync(db);

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);
        var verificationToken = EmailVerificationToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(24));
        verificationToken.MarkAsUsed();
        db.EmailVerificationTokens.Add(verificationToken);
        await db.SaveChangesAsync();

        var handler = new VerifyEmailCommandHandler(db, new FakeSecurityEventLogger());
        var result = await handler.Handle(new VerifyEmailCommand(Plaintext), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidToken.Code, result.Error.Code);
    }
}
