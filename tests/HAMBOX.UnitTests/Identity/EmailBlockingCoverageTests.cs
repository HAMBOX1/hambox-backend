using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.ForgotPassword;
using HAMBOX.Modules.Identity.Application.Features.Login;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Coverage added by the 2026-08-30 Security Center customer-blocking review.
///
/// <see cref="LoginCommandHandler"/> already has broad failure-path coverage in
/// <see cref="LoginAccountEnumerationTests"/>, but none of it exercises the actual
/// <c>IsEmailBlockedAsync</c> branch specifically (every fake there always returns <c>false</c>) —
/// <see cref="Login_BlockedEmail_IsRejected_WithGenericInvalidCredentials"/> closes that gap and
/// passes, confirming the one enforcement point that was live-verified end-to-end in the
/// conversation's manual HTTP testing.
///
/// <see cref="ForgotPasswordCommandHandler"/> has no <see cref="ISecurityBlocklistService"/>
/// dependency at all — <see cref="ForgotPassword_BlockedEmail_StillIssuesAWorkingResetToken_DocumentsTheGap"/>
/// encodes the secure expectation (a blocked account's email should not be able to obtain a working
/// password-reset token) and is expected to FAIL until that check is added; it is intentionally not
/// written to match the current behavior, since that would defeat the point of a regression test
/// documenting a real gap. <see cref="BlockedEmail"/>'s own XML doc claims blocking covers
/// "registration, login, and password reset" — this test proves the password-reset part of that
/// claim does not hold today.
/// </summary>
public sealed class EmailBlockingCoverageTests
{
    private static IdentityDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public async Task Login_BlockedEmail_IsRejected_WithGenericInvalidCredentials()
    {
        await using var db = CreateDb();
        var user = ApplicationUser.Create("blocked-user@example.com", "hashed", "Test", "User");
        user.ConfirmEmail();
        user.Activate();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(
            db,
            new AlwaysMatchesPasswordHasher(),
            new NeverAdminAccessResolver(),
            new UnreachableAuthTokenIssuer(),
            new StubPlatformSettingsProvider(),
            new BlocksExactly(user.Email),
            new NoOpSecurityEventLogger(),
            new StubClientInfoParser(),
            new UnreachableTrustedDeviceService(),
            new StubLoginRiskScorer());

        var result = await handler.Handle(
            new LoginCommand(user.Email, "any-password", "203.0.113.1", "test-agent"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidCredentials.Code, result.Error.Code);
    }

    [Fact]
    public async Task ForgotPassword_BlockedEmail_StillIssuesAWorkingResetToken_DocumentsTheGap()
    {
        await using var db = CreateDb();
        var user = ApplicationUser.Create("blocked-user@example.com", "hashed", "Test", "User");
        db.Users.Add(user);
        db.BlockedEmails.Add(BlockedEmail.Create(user.Email, "integration-test-seed"));
        await db.SaveChangesAsync();

        // ForgotPasswordCommandHandler takes no ISecurityBlocklistService — there is nothing to
        // fake "blocked" here; the seeded BlockedEmail row above is exactly what the real admin
        // Security Center API would have written, and the handler simply never looks at it.
        var handler = new ForgotPasswordCommandHandler(db, new SequentialTokenGenerator("reset-token-value"), new RecordingEmailService());

        var result = await handler.Handle(
            new ForgotPasswordCommand(user.Email, "203.0.113.1", "turnstile-token"), CancellationToken.None);

        // Secure expectation: a blocked account must not receive a usable reset token. Today it
        // does (Result.Success() and a real PasswordResetTokens row), so this assertion fails —
        // that failure IS the documented finding.
        Assert.Empty(db.PasswordResetTokens);
    }

    private sealed class AlwaysMatchesPasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => password;
        public bool VerifyPassword(string hashedPassword, string providedPassword) => true;
    }

    private sealed class NeverAdminAccessResolver : IAdminAccessResolver
    {
        public Task<bool> HasAdminPortalAccessAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class UnreachableAuthTokenIssuer : IAuthTokenIssuer
    {
        public Task<Result<AuthTokenResponse>> IssueAsync(
            ApplicationUser user, string authContext, bool otpVerified, string ipAddress, string userAgent,
            bool rememberMe = false, LoginContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The blocked-email path must never reach token issuance.");
    }

    private sealed class StubPlatformSettingsProvider : IPlatformSettingsProvider
    {
        private static NotSupportedException NotNeeded() => new("Not needed by this test.");
        public Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SecuritySettingsPayload(5, 15, true));
        public Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<OtpSettingsPayload> GetOtpAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<InventorySettingsPayload> GetInventoryAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<CurrencySettingsPayload> GetCurrencyAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<MediaSettingsPayload> GetMediaAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<MaintenanceSettingsPayload> GetMaintenanceAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<bool> VerifyMaintenanceBypassPasswordAsync(string password, CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<StorefrontContentSettingsPayload> GetStorefrontAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<PublicPlatformSettingsDto> GetPublicSettingsAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public void InvalidateCache(string? categoryKey = null) => throw NotNeeded();
    }

    /// <summary>Mirrors the real <c>ISecurityBlocklistService.IsEmailBlockedAsync</c> normalization
    /// (trim + lowercase, exact match) closely enough to prove the handler's branch, without
    /// pulling in the real EF-backed implementation.</summary>
    private sealed class BlocksExactly(string blockedEmail) : ISecurityBlocklistService
    {
        private readonly string _blocked = blockedEmail.Trim().ToLowerInvariant();
        public Task<bool> IsEmailBlockedAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(email.Trim().ToLowerInvariant() == _blocked);
        public Task<bool> IsIpBlockedAsync(string ipAddress, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<CountryRestrictionStatus> GetCountryStatusAsync(string countryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(CountryRestrictionStatus.Allowed);
        public void InvalidateCache() { }
    }

    private sealed class NoOpSecurityEventLogger : ISecurityEventLogger
    {
        public Task LogAsync(
            SecurityEventType eventType, SecurityEventSeverity severity, string description,
            Guid? actorUserId = null, Guid? targetUserId = null, string? ipAddress = null, string? country = null,
            string? userAgent = null, string? correlationId = null, string? city = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubClientInfoParser : IClientInfoParser
    {
        public (string? BrowserName, string? OsName, string? DeviceName) ParseUserAgent(string userAgent) => ("Browser", "OS", "Desktop");
    }

    private sealed class UnreachableTrustedDeviceService : ITrustedDeviceService
    {
        public Task<bool> IsDeviceBlockedAsync(Guid userId, string fingerprint, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task<bool> RecordLoginAsync(Guid userId, string fingerprint, LoginContext context, string ipAddress, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The blocked-email path must never reach device recording.");
    }

    private sealed class StubLoginRiskScorer : ILoginRiskScorer
    {
        public SecurityEventSeverity ScoreSuccessfulLogin(bool isNewDevice, bool isNewCountry) =>
            throw new InvalidOperationException("The blocked-email path must never reach success scoring.");
        public SecurityEventSeverity ScoreFailedPassword(int accessFailedCount, int maxFailedAccessAttempts) => SecurityEventSeverity.Medium;
    }

    private sealed class SequentialTokenGenerator(params string[] values) : ITokenGenerator
    {
        private readonly Queue<string> _values = new(values);
        public string GenerateSecureToken() => _values.Count > 0 ? _values.Dequeue() : Guid.NewGuid().ToString("N");
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public Task<bool> SendEmailVerificationAsync(Guid userId, string email, DateTimeOffset expiresAt, string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> SendPasswordResetAsync(Guid userId, string email, DateTimeOffset expiresAt, string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> SendAdminLoginOtpAsync(Guid userId, string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task SendTemplatedEmailAsync(string toEmail, string subject, string htmlBody, string? correlationId = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
