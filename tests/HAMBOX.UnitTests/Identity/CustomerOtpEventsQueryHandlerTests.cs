using HAMBOX.Modules.Identity.Application.Features.Security.OtpEvents;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Coverage for the admin/support read model over <see cref="CustomerOtpAuditLog"/> — the surface
/// that must answer "what OTP action happened to this customer, when, why, and what was the
/// result" without ever exposing the token/code value itself.
/// </summary>
public sealed class CustomerOtpEventsQueryHandlerTests
{
    private static IdentityDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new IdentityDbContext(options);
    }

    [Fact]
    public void CustomerOtpEventDto_HasNoTokenOrCodeCarryingProperty()
    {
        // Structural guard: even if a future edit adds a field to the DTO, this fails loudly if
        // anyone ever names it "Token"/"Code"/"Secret" — the shape support/admin tooling consumes
        // must never be able to carry the OTP value, by construction.
        var forbidden = new[] { "token", "code", "secret", "plaintext" };

        foreach (var property in typeof(HAMBOX.Modules.Identity.Application.Contracts.CustomerOtpEventDto).GetProperties())
        {
            var lowerName = property.Name.ToLowerInvariant();
            Assert.DoesNotContain(forbidden, f => lowerName.Contains(f));
        }
    }

    [Fact]
    public async Task Handle_ReturnsEvents_WithUserEmailResolved_NewestFirst_AndNoTokenValueAnywhere()
    {
        await using var db = CreateDb();
        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "hash", "Test", "User");
        db.Users.Add(user);

        var older = CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.EmailVerification, CustomerOtpEventStatus.Pending,
            DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(22), user.Id);
        var newer = CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.PasswordReset, CustomerOtpEventStatus.Used,
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(55), user.Id,
            usedOnUtc: DateTimeOffset.UtcNow);
        db.CustomerOtpAuditLogs.AddRange(older, newer);
        await db.SaveChangesAsync();

        var handler = new GetCustomerOtpEventsQueryHandler(db);
        var result = await handler.Handle(new GetCustomerOtpEventsQuery(1, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(newer.Id, result.Value.Items.First().Id); // newest-first
        Assert.All(result.Value.Items, item => Assert.Equal(user.Email, item.UserEmail));
    }

    [Fact]
    public async Task Handle_FiltersByPurposeAndStatus()
    {
        await using var db = CreateDb();
        var user = ApplicationUser.Create($"user-{Guid.NewGuid():N}@example.com", "hash", "Test", "User");
        db.Users.Add(user);

        db.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.EmailVerification, CustomerOtpEventStatus.Failed, null, null, user.Id));
        db.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.PasswordReset, CustomerOtpEventStatus.Used,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), user.Id));
        await db.SaveChangesAsync();

        var handler = new GetCustomerOtpEventsQueryHandler(db);
        var result = await handler.Handle(
            new GetCustomerOtpEventsQuery(1, 20, Purpose: "EmailVerification", Status: "Failed"), CancellationToken.None);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal("EmailVerification", item.Purpose);
        Assert.Equal("Failed", item.Status);
    }
}
