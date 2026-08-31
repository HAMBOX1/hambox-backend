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
/// Proves <see cref="LoginCommandHandler"/> actually consults
/// <see cref="SecuritySettingsPayload.RequireEmailVerification"/> (Admin Settings → Security) instead
/// of unconditionally rejecting unverified customers. A "Pending"-status account exclusively means
/// "not yet email-verified" in this codebase (<see cref="ApplicationUser.Activate"/> is only ever
/// called alongside <see cref="ApplicationUser.ConfirmEmail"/>), so disabling the flag must also let
/// the account past the account-status gate, not just the explicit EmailConfirmed check.
/// </summary>
public sealed class RequireEmailVerificationLoginTests
{
    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new IdentityDbContext(options);
    }

    private static LoginCommandHandler CreateHandler(
        IIdentityDbContext dbContext,
        bool requireEmailVerification,
        int maxFailedAccessAttempts = 5) =>
        new(
            dbContext,
            new FakePasswordHasher(),
            new FakeAdminAccessResolver(),
            new FakeAuthTokenIssuer(),
            new FakePlatformSettingsProvider(requireEmailVerification, maxFailedAccessAttempts),
            new FakeSecurityBlocklistService(),
            new FakeSecurityEventLogger(),
            new FakeClientInfoParser(),
            new FakeTrustedDeviceService(),
            new FakeLoginRiskScorer());

    private static async Task<ApplicationUser> SeedUserAsync(IdentityDbContext db, Action<ApplicationUser>? configure = null)
    {
        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "correct-password", "Test", "User");
        configure?.Invoke(user);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static LoginCommand CommandFor(string email, string password = "correct-password") =>
        new(email, password, "203.0.113.1", "test-agent");

    [Fact]
    public async Task FlagOn_UnverifiedUser_IsRejectedWithEmailNotConfirmed()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db); // Create() leaves Pending + EmailConfirmed = false
        var handler = CreateHandler(db, requireEmailVerification: true);

        var result = await handler.Handle(CommandFor(user.Email), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.EmailNotConfirmed.Code, result.Error.Code);
    }

    [Fact]
    public async Task FlagOff_UnverifiedUser_IsAllowedToLogIn()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db); // Pending + EmailConfirmed = false
        var handler = CreateHandler(db, requireEmailVerification: false);

        var result = await handler.Handle(CommandFor(user.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VerifiedUser_IsAllowedRegardlessOfFlag(bool requireEmailVerification)
    {
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db, u =>
        {
            u.ConfirmEmail();
            u.Activate();
        });
        var handler = CreateHandler(db, requireEmailVerification);

        var result = await handler.Handle(CommandFor(user.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task FlagOff_SuspendedUser_IsStillRejected()
    {
        // Suspended is an explicit, separate status reached only via Suspend() — never conflated
        // with "Pending because unverified" — so disabling email verification must not open this gate.
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db, u =>
        {
            u.ConfirmEmail();
            u.Activate();
            u.Suspend("policy violation");
        });
        var handler = CreateHandler(db, requireEmailVerification: false);

        var result = await handler.Handle(CommandFor(user.Email), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.AccountNotActive.Code, result.Error.Code);
    }

    [Fact]
    public async Task FlagOff_WrongPassword_StillLocksOutAfterMaxAttempts()
    {
        // Lockout/failed-attempt tracking must remain intact regardless of the verification flag.
        await using var db = CreateDbContext();
        var user = await SeedUserAsync(db, u =>
        {
            u.ConfirmEmail();
            u.Activate();
        });
        var handler = CreateHandler(db, requireEmailVerification: false, maxFailedAccessAttempts: 1);

        var result = await handler.Handle(CommandFor(user.Email, password: "wrong-password"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.AccountLocked.Code, result.Error.Code);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => password;
        public bool VerifyPassword(string hashedPassword, string providedPassword) => hashedPassword == providedPassword;
    }

    private sealed class FakeAdminAccessResolver : IAdminAccessResolver
    {
        public Task<bool> HasAdminPortalAccessAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeAuthTokenIssuer : IAuthTokenIssuer
    {
        public Task<Result<AuthTokenResponse>> IssueAsync(
            ApplicationUser user, string authContext, bool otpVerified, string ipAddress, string userAgent,
            bool rememberMe = false, LoginContext? context = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new AuthTokenResponse(
                "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddDays(30))));
    }

    private sealed class FakePlatformSettingsProvider(bool requireEmailVerification, int maxFailedAccessAttempts)
        : IPlatformSettingsProvider
    {
        private static NotSupportedException NotNeeded() => new("Not needed by this test.");

        public Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SecuritySettingsPayload(
                MaxFailedAccessAttempts: maxFailedAccessAttempts,
                LockoutDurationMinutes: 15,
                RequireEmailVerification: requireEmailVerification));

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
            Task.FromResult(false);
    }

    private sealed class FakeLoginRiskScorer : ILoginRiskScorer
    {
        public SecurityEventSeverity ScoreSuccessfulLogin(bool isNewDevice, bool isNewCountry) =>
            SecurityEventSeverity.Low;

        public SecurityEventSeverity ScoreFailedPassword(int accessFailedCount, int maxFailedAccessAttempts) =>
            SecurityEventSeverity.Medium;
    }
}
