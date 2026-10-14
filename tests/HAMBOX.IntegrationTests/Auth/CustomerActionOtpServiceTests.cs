using System.Text.RegularExpressions;
using HAMBOX.Application.Security;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HAMBOX.IntegrationTests.Auth;

/// <summary>
/// The real e-mailed one-time-code service behind "show my license key" (audit R9), against a real database:
/// hashing at rest, single use, expiry, resend cool-down, lock-out and per-key scoping.
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class CustomerActionOtpServiceTests : IAsyncLifetime
{
    private const string Purpose = "LicenseKeyReveal";

    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private readonly CapturingEmailService _email = new();
    private WebApplicationFactory<Program> _host = null!;
    private string _userId = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();

        _host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(_email);
        }));

        using var scope = _host.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = ApplicationUser.Create($"otp-{Guid.NewGuid():N}@example.com", hasher.HashPassword("TestPassword123!"), "Otp", "Tester");
        user.ConfirmEmail();
        user.Activate();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id.ToString();
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private ICustomerActionOtpService Service(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICustomerActionOtpService>();

    [Fact]
    public async Task IssuedCode_IsEmailed_StoredOnlyAsAHash_AndWorksExactlyOnce()
    {
        using var scope = _host.Services.CreateScope();
        var service = Service(scope);

        var issued = await service.IssueAsync(_userId, Purpose, "key-1");
        Assert.Equal(CustomerActionOtpIssueStatus.Sent, issued.Status);
        var code = _email.LastCode!;
        Assert.Matches("^[0-9]{6}$", code);

        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var stored = await db.CustomerActionChallenges.AsNoTracking().SingleAsync(c => c.ScopeKey == "key-1");
        Assert.DoesNotContain(code, stored.CodeHash);
        Assert.NotEqual(code, stored.CodeHash);

        Assert.Equal(CustomerActionOtpVerifyStatus.Invalid, await service.VerifyAsync(_userId, Purpose, "key-1", "000000"));
        Assert.Equal(CustomerActionOtpVerifyStatus.Verified, await service.VerifyAsync(_userId, Purpose, "key-1", code));
        Assert.Equal(CustomerActionOtpVerifyStatus.NotRequested, await service.VerifyAsync(_userId, Purpose, "key-1", code));
    }

    [Fact]
    public async Task ACodeForOneKey_NeverOpensAnotherKey()
    {
        using var scope = _host.Services.CreateScope();
        var service = Service(scope);

        await service.IssueAsync(_userId, Purpose, "key-A");
        var code = _email.LastCode!;

        Assert.Equal(CustomerActionOtpVerifyStatus.NotRequested, await service.VerifyAsync(_userId, Purpose, "key-B", code));
        Assert.Equal(CustomerActionOtpVerifyStatus.Verified, await service.VerifyAsync(_userId, Purpose, "key-A", code));
    }

    [Fact]
    public async Task AskingAgainStraightAway_IsHeldBackByTheResendCooldown()
    {
        using var scope = _host.Services.CreateScope();
        var service = Service(scope);

        var first = await service.IssueAsync(_userId, Purpose, "key-1");
        var second = await service.IssueAsync(_userId, Purpose, "key-1");

        Assert.Equal(CustomerActionOtpIssueStatus.Sent, first.Status);
        Assert.Equal(CustomerActionOtpIssueStatus.CooldownActive, second.Status);
        Assert.NotNull(second.ResendAvailableAt);
        Assert.Equal(1, _email.Sent);
    }

    [Fact]
    public async Task TooManyWrongCodes_LockTheChallenge_EvenAgainstTheRightCode()
    {
        using var scope = _host.Services.CreateScope();
        var service = Service(scope);

        await service.IssueAsync(_userId, Purpose, "key-1");
        var code = _email.LastCode!;

        // Platform default: 3 attempts.
        Assert.Equal(CustomerActionOtpVerifyStatus.Invalid, await service.VerifyAsync(_userId, Purpose, "key-1", "000000"));
        Assert.Equal(CustomerActionOtpVerifyStatus.Invalid, await service.VerifyAsync(_userId, Purpose, "key-1", "000001"));
        Assert.Equal(CustomerActionOtpVerifyStatus.Locked, await service.VerifyAsync(_userId, Purpose, "key-1", "000002"));

        Assert.Equal(CustomerActionOtpVerifyStatus.Locked, await service.VerifyAsync(_userId, Purpose, "key-1", code));
    }

    [Fact]
    public async Task AnExpiredCode_IsRefused()
    {
        using var scope = _host.Services.CreateScope();
        var service = Service(scope);

        await service.IssueAsync(_userId, Purpose, "key-1");
        var code = _email.LastCode!;

        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [identity].[CustomerActionChallenges] SET [ExpiresOnUtc] = DATEADD(minute, -1, SYSDATETIMEOFFSET())");
        db.ChangeTracker.Clear(); // the service shares this scoped context; re-read the row we just changed

        Assert.Equal(CustomerActionOtpVerifyStatus.Expired, await service.VerifyAsync(_userId, Purpose, "key-1", code));
    }

    [Fact]
    public async Task WhenTheEmailCannotBeDelivered_NoUsableCodeIsLeftBehind()
    {
        _email.Fail = true;
        using var scope = _host.Services.CreateScope();
        var service = Service(scope);

        var issued = await service.IssueAsync(_userId, Purpose, "key-1");

        Assert.Equal(CustomerActionOtpIssueStatus.DeliveryFailed, issued.Status);
        Assert.Equal(CustomerActionOtpVerifyStatus.NotRequested,
            await service.VerifyAsync(_userId, Purpose, "key-1", "123456"));
    }

    [Fact]
    public async Task AnUnknownUser_GetsNoCode()
    {
        using var scope = _host.Services.CreateScope();
        var service = Service(scope);

        var issued = await service.IssueAsync(Guid.NewGuid().ToString(), Purpose, "key-1");

        Assert.Equal(CustomerActionOtpIssueStatus.UserNotFound, issued.Status);
        Assert.Equal(0, _email.Sent);
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public bool Fail { get; set; }

        public int Sent { get; private set; }

        public string? LastCode { get; private set; }

        public Task SendTemplatedEmailAsync(
            string toEmail, string subject, string htmlBody, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            if (Fail)
            {
                throw new InvalidOperationException("SMTP is down");
            }

            Sent++;
            LastCode = Regex.Match(htmlBody, @"\b\d{6}\b").Value;
            return Task.CompletedTask;
        }

        public Task<bool> SendEmailVerificationAsync(Guid userId, string email, DateTimeOffset expiresAt, string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> SendPasswordResetAsync(Guid userId, string email, DateTimeOffset expiresAt, string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> SendAdminLoginOtpAsync(Guid userId, string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
