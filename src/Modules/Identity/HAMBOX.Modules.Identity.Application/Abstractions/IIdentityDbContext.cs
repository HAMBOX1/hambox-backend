using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Permissions;
using HAMBOX.Modules.Identity.Domain.PlatformSettings;
using HAMBOX.Modules.Identity.Domain.Roles;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Defines the database context contract for the Identity module.
/// </summary>
public interface IIdentityDbContext
{
    /// <summary>
    /// Gets the users database set.
    /// </summary>
    DbSet<ApplicationUser> Users { get; }

    /// <summary>
    /// Gets the roles database set.
    /// </summary>
    DbSet<ApplicationRole> Roles { get; }

    /// <summary>
    /// Gets the user roles database set.
    /// </summary>
    DbSet<UserRole> UserRoles { get; }

    /// <summary>
    /// Gets the permission groups database set.
    /// </summary>
    DbSet<PermissionGroup> PermissionGroups { get; }

    /// <summary>
    /// Gets the permissions database set.
    /// </summary>
    DbSet<Permission> Permissions { get; }

    /// <summary>
    /// Gets the role permissions database set.
    /// </summary>
    DbSet<RolePermission> RolePermissions { get; }

    /// <summary>
    /// Gets the authorization audit logs database set.
    /// </summary>
    DbSet<AuthorizationAuditLog> AuthorizationAuditLogs { get; }

    /// <summary>
    /// Gets the refresh tokens database set.
    /// </summary>
    DbSet<RefreshToken> RefreshTokens { get; }

    /// <summary>
    /// Gets the email verification tokens database set.
    /// </summary>
    DbSet<EmailVerificationToken> EmailVerificationTokens { get; }

    /// <summary>
    /// Gets the password reset tokens database set.
    /// </summary>
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    /// <summary>
    /// Gets the user sessions database set.
    /// </summary>
    DbSet<UserSession> UserSessions { get; }

    /// <summary>
    /// Gets the login history database set.
    /// </summary>
    DbSet<LoginHistory> LoginHistory { get; }

    /// <summary>
    /// Gets the admin login OTP challenges database set.
    /// </summary>
    DbSet<AdminLoginChallenge> AdminLoginChallenges { get; }

    /// <summary>
    /// Gets the admin OTP audit logs database set.
    /// </summary>
    DbSet<AdminOtpAuditLog> AdminOtpAuditLogs { get; }

    /// <summary>
    /// Gets the customer OTP/verification-token audit logs database set.
    /// </summary>
    DbSet<CustomerOtpAuditLog> CustomerOtpAuditLogs { get; }

    /// <summary>
    /// Gets the platform settings categories database set.
    /// </summary>
    DbSet<PlatformSettingsCategory> PlatformSettingsCategories { get; }

    /// <summary>
    /// Gets the platform settings audit logs database set.
    /// </summary>
    DbSet<PlatformSettingsAuditLog> PlatformSettingsAuditLogs { get; }

    /// <summary>
    /// Gets the Security Center blocked emails/domains database set.
    /// </summary>
    DbSet<BlockedEmail> BlockedEmails { get; }

    /// <summary>
    /// Gets the Security Center blocked IP addresses/ranges database set.
    /// </summary>
    DbSet<BlockedIp> BlockedIps { get; }

    /// <summary>
    /// Gets the Security Center country restrictions database set.
    /// </summary>
    DbSet<CountryRestriction> CountryRestrictions { get; }

    /// <summary>
    /// Gets the Security Center blocked devices database set (architecture-only, no live usage).
    /// </summary>
    DbSet<BlockedDevice> BlockedDevices { get; }

    /// <summary>
    /// Gets the Security Center security event log database set.
    /// </summary>
    DbSet<SecurityEventLog> SecurityEventLogs { get; }

    /// <summary>
    /// Gets the trusted/recognized devices database set.
    /// </summary>
    DbSet<TrustedDevice> TrustedDevices { get; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous save operation, containing the number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
