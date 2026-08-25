using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Login;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Proves <see cref="LoginCommandHandler"/> returns the exact same <see cref="Error"/>
/// (<see cref="IdentityErrors.InvalidCredentials"/>) for every pre-token-issuance failure branch —
/// nonexistent email, unconfirmed email, inactive account, pre-locked account, wrong password, and
/// wrong password that itself triggers lockout — so nothing externally observable (HTTP status,
/// error code, or localized message, since <c>LocalizedEndpointResults.FromResult</c> maps 1:1 from
/// <see cref="Result.Error"/>) lets a caller distinguish "this email isn't registered" from "this
/// email is registered but something else is wrong", closing the account-enumeration gap. Internal
/// logging (LoginHistory, SecurityEventLogger) is intentionally left untouched by this fix and not
/// re-asserted here — only the caller-visible Result is in scope.
/// </summary>
public sealed class LoginAccountEnumerationTests
{
    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new IdentityDbContext(options);
    }

    private static LoginCommandHandler CreateHandler(IIdentityDbContext dbContext, bool passwordMatches) =>
        new(
            dbContext,
            new FakePasswordHasher(passwordMatches),
            new FakeAdminAccessResolver(),
            new NeverInvokedAuthTokenIssuer(),
            new FakePlatformSettingsProvider(),
            new FakeSecurityBlocklistService(),
            new FakeSecurityEventLogger(),
            new FakeClientInfoParser(),
            new FakeTrustedDeviceService(),
            new FakeLoginRiskScorer());

    private static async Task<ApplicationUser> SeedUserAsync(IdentityDbContext db, Action<ApplicationUser>? configure = null)
    {
        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "hashed", "Test", "User");
        configure?.Invoke(user);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static LoginCommand CommandFor(string email) =>
        new(email, "wrong-or-right-password", "203.0.113.1", "test-agent");

    private static void AssertGenericInvalidCredentials(Result<AuthTokenResponse> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidCredentials.Code, result.Error.Code);
    }

    [Fact]
    public async Task NonexistentEmail_ReturnsGenericInvalidCredentials()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db, passwordMatches: false);

        var result = await handler.Handle(CommandFor("nobody@example.com"), CancellationToken.None);

        AssertGenericInvalidCredentials(result);
    }

    [Fact]
    public async Task UnconfirmedEmail_ReturnsGenericInvalidCredentials_NotEmailNotConfirmed()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db); // Create() leaves EmailConfirmed = false
        var handler = CreateHandler(db, passwordMatches: true);

        var result = await handler.Handle(CommandFor(user.Email), CancellationToken.None);

        AssertGenericInvalidCredentials(result);
    }

    [Fact]
    public async Task InactiveAccount_ReturnsGenericInvalidCredentials_NotAccountNotActive()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db, u => u.ConfirmEmail()); // confirmed but still Pending (not Active)
        var handler = CreateHandler(db, passwordMatches: true);

        var result = await handler.Handle(CommandFor(user.Email), CancellationToken.None);

        AssertGenericInvalidCredentials(result);
    }

    [Fact]
    public async Task PreLockedAccount_ReturnsGenericInvalidCredentials_NotAccountLocked()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db, u =>
        {
            u.ConfirmEmail();
            u.Activate();
            u.RecordAccessFailure(maxFailedAttempts: 1, lockoutDuration: TimeSpan.FromMinutes(15));
        });
        var handler = CreateHandler(db, passwordMatches: true);

        var result = await handler.Handle(CommandFor(user.Email), CancellationToken.None);

        AssertGenericInvalidCredentials(result);
    }

    [Fact]
    public async Task WrongPassword_ActiveConfirmedAccount_ReturnsGenericInvalidCredentials()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db, u =>
        {
            u.ConfirmEmail();
            u.Activate();
        });
        var handler = CreateHandler(db, passwordMatches: false);

        var result = await handler.Handle(CommandFor(user.Email), CancellationToken.None);

        AssertGenericInvalidCredentials(result);
    }

    [Fact]
    public async Task WrongPassword_ThatItselfTriggersLockout_StillReturnsGenericInvalidCredentials()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db, u =>
        {
            u.ConfirmEmail();
            u.Activate();
        });
        // FakePlatformSettingsProvider.GetSecurityAsync returns MaxFailedAccessAttempts = 1, so this
        // single wrong-password attempt locks the account inside the handler itself.
        var handler = CreateHandler(db, passwordMatches: false);

        var result = await handler.Handle(CommandFor(user.Email), CancellationToken.None);

        AssertGenericInvalidCredentials(result);
    }

    private sealed class FakePasswordHasher(bool matches) : IPasswordHasher
    {
        public string HashPassword(string password) => password;
        public bool VerifyPassword(string hashedPassword, string providedPassword) => matches;
    }

    private sealed class FakeAdminAccessResolver : IAdminAccessResolver
    {
        public Task<bool> HasAdminPortalAccessAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class NeverInvokedAuthTokenIssuer : IAuthTokenIssuer
    {
        public Task<Result<AuthTokenResponse>> IssueAsync(
            ApplicationUser user, string authContext, bool otpVerified, string ipAddress, string userAgent,
            bool rememberMe = false, LoginContext? context = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Every scenario in this test class is a failure path — token issuance must never be reached.");
    }

    private sealed class FakePlatformSettingsProvider : IPlatformSettingsProvider
    {
        private static NotSupportedException NotNeeded() => new("Not needed by this test.");

        public Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SecuritySettingsPayload(
                MaxFailedAccessAttempts: 1,
                LockoutDurationMinutes: 15,
                RequireEmailVerification: true,
                EnforceHttps: true));

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

    private sealed class FakeSecurityBlocklistService : ISecurityBlocklistService
    {
        public Task<bool> IsEmailBlockedAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsIpBlockedAsync(string ipAddress, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<CountryRestrictionStatus> GetCountryStatusAsync(string countryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(CountryRestrictionStatus.Allowed);
        public void InvalidateCache()
        {
        }
    }

    private sealed class FakeSecurityEventLogger : ISecurityEventLogger
    {
        public Task LogAsync(
            SecurityEventType eventType, SecurityEventSeverity severity, string description,
            Guid? actorUserId = null, Guid? targetUserId = null, string? ipAddress = null, string? country = null,
            string? userAgent = null, string? correlationId = null, string? city = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeClientInfoParser : IClientInfoParser
    {
        public (string? BrowserName, string? OsName, string? DeviceName) ParseUserAgent(string userAgent) =>
            ("TestBrowser", "TestOS", "Desktop");
    }

    private sealed class FakeTrustedDeviceService : ITrustedDeviceService
    {
        public Task<bool> IsDeviceBlockedAsync(Guid userId, string fingerprint, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> RecordLoginAsync(
            Guid userId, string fingerprint, LoginContext context, string ipAddress,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Every scenario in this test class is a failure path — device recording only happens on success.");
    }

    private sealed class FakeLoginRiskScorer : ILoginRiskScorer
    {
        public SecurityEventSeverity ScoreSuccessfulLogin(bool isNewDevice, bool isNewCountry) =>
            throw new InvalidOperationException(
                "Every scenario in this test class is a failure path — success scoring is never reached.");

        public SecurityEventSeverity ScoreFailedPassword(int accessFailedCount, int maxFailedAccessAttempts) =>
            SecurityEventSeverity.Medium;
    }
}
