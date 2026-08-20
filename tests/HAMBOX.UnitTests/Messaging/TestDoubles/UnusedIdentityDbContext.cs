using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Permissions;
using HAMBOX.Modules.Identity.Domain.PlatformSettings;
using HAMBOX.Modules.Identity.Domain.Roles;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Messaging.TestDoubles;

/// <summary>Not exercised by the Browse/Search/Cart scenario — those states never link an account.
/// Every member throws so an unexpected call fails loudly instead of silently touching a real EF model
/// this test project doesn't otherwise need to build.</summary>
internal sealed class UnusedIdentityDbContext : IIdentityDbContext
{
    private static NotSupportedException NotNeeded() => new("Not needed by these tests.");

    public DbSet<ApplicationUser> Users => throw NotNeeded();
    public DbSet<ApplicationRole> Roles => throw NotNeeded();
    public DbSet<UserRole> UserRoles => throw NotNeeded();
    public DbSet<PermissionGroup> PermissionGroups => throw NotNeeded();
    public DbSet<Permission> Permissions => throw NotNeeded();
    public DbSet<RolePermission> RolePermissions => throw NotNeeded();
    public DbSet<AuthorizationAuditLog> AuthorizationAuditLogs => throw NotNeeded();
    public DbSet<RefreshToken> RefreshTokens => throw NotNeeded();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => throw NotNeeded();
    public DbSet<PasswordResetToken> PasswordResetTokens => throw NotNeeded();
    public DbSet<UserSession> UserSessions => throw NotNeeded();
    public DbSet<LoginHistory> LoginHistory => throw NotNeeded();
    public DbSet<AdminLoginChallenge> AdminLoginChallenges => throw NotNeeded();
    public DbSet<AdminOtpAuditLog> AdminOtpAuditLogs => throw NotNeeded();
    public DbSet<PlatformSettingsCategory> PlatformSettingsCategories => throw NotNeeded();
    public DbSet<PlatformSettingsAuditLog> PlatformSettingsAuditLogs => throw NotNeeded();
    public DbSet<BlockedEmail> BlockedEmails => throw NotNeeded();
    public DbSet<BlockedIp> BlockedIps => throw NotNeeded();
    public DbSet<CountryRestriction> CountryRestrictions => throw NotNeeded();
    public DbSet<BlockedDevice> BlockedDevices => throw NotNeeded();
    public DbSet<SecurityEventLog> SecurityEventLogs => throw NotNeeded();
    public DbSet<TrustedDevice> TrustedDevices => throw NotNeeded();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
}
