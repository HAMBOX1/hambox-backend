using System.Security.Cryptography;
using System.Text;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.ResetPassword;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Proves <see cref="PasswordResetToken"/> and <see cref="EmailVerificationToken"/> now store only a
/// SHA-256 lookup hash of the plaintext token — mirroring <see cref="HAMBOX.Modules.Identity.Domain.Tokens.RefreshToken"/>'s
/// already-correct pattern — never the plaintext value itself, and that the reset flow still works
/// end-to-end for a caller holding the plaintext token from the email.
/// </summary>
public sealed class AccountTokenHashingTests
{
    private const string Plaintext = "a-cryptographically-random-looking-token-value-123";

    private static string IndependentSha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

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

    /// <summary>
    /// End-to-end through the real handler + a real (InMemory) <see cref="IdentityDbContext"/>: the row
    /// persisted by <see cref="PasswordResetToken.Create"/> never contains the plaintext token (proving
    /// it truly isn't written to the database, not just that the in-memory object hashes it), and the
    /// caller who holds the plaintext (as delivered by the reset email) can still successfully reset
    /// the password with it.
    /// </summary>
    [Fact]
    public async Task ResetPasswordCommandHandler_PlaintextNeverPersisted_ButValidTokenStillWorks()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new IdentityDbContext(options);

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);

        var resetToken = PasswordResetToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(1));
        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        // Prove it directly against what's actually stored, not the in-memory entity reference.
        var persisted = await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.Id == resetToken.Id);
        Assert.NotEqual(Plaintext, persisted.Token);
        Assert.DoesNotContain(Plaintext, persisted.Token, StringComparison.Ordinal);

        var handler = new ResetPasswordCommandHandler(db, new FakePasswordHasher());
        var result = await handler.Handle(new ResetPasswordCommand(Plaintext, "NewPassword123!"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var reloadedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal("hashed:NewPassword123!", reloadedUser.PasswordHash);
    }

    [Fact]
    public async Task ResetPasswordCommandHandler_WrongToken_FailsWithInvalidToken()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new IdentityDbContext(options);

        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "old-hash", "Test", "User");
        db.Users.Add(user);
        db.PasswordResetTokens.Add(PasswordResetToken.Create(user.Id, Plaintext, DateTimeOffset.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(db, new FakePasswordHasher());
        var result = await handler.Handle(
            new ResetPasswordCommand("this-is-not-the-right-token", "NewPassword123!"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidToken.Code, result.Error.Code);
    }

    private sealed class FakePasswordHasher : HAMBOX.Modules.Identity.Application.Abstractions.IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";
        public bool VerifyPassword(string hashedPassword, string providedPassword) => hashedPassword == $"hashed:{providedPassword}";
    }
}
