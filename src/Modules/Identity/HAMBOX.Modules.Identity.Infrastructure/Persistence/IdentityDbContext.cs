using HAMBOX.Domain.Entities;
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

namespace HAMBOX.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Represents the Entity Framework Core database context for the Identity module.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) 
    : DbContext(options), IIdentityDbContext
{
    /// <summary>
    /// Gets or sets the users table.
    /// </summary>
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    /// <summary>
    /// Gets or sets the roles table.
    /// </summary>
    public DbSet<ApplicationRole> Roles => Set<ApplicationRole>();

    /// <summary>
    /// Gets or sets the permission groups table.
    /// </summary>
    public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();

    /// <summary>
    /// Gets or sets the permissions table.
    /// </summary>
    public DbSet<Permission> Permissions => Set<Permission>();

    /// <summary>
    /// Gets or sets the role permissions join table.
    /// </summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>
    /// Gets or sets the authorization audit logs table.
    /// </summary>
    public DbSet<AuthorizationAuditLog> AuthorizationAuditLogs => Set<AuthorizationAuditLog>();

    /// <summary>
    /// Gets or sets the refresh tokens table.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Gets or sets the email verification tokens table.
    /// </summary>
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    /// <summary>
    /// Gets or sets the password reset tokens table.
    /// </summary>
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    /// <summary>
    /// Gets or sets the user sessions table.
    /// </summary>
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    /// <summary>
    /// Gets or sets the login history table.
    /// </summary>
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();

    /// <summary>
    /// Gets or sets the admin login OTP challenges table.
    /// </summary>
    public DbSet<AdminLoginChallenge> AdminLoginChallenges => Set<AdminLoginChallenge>();

    /// <summary>
    /// Gets or sets the admin OTP audit logs table.
    /// </summary>
    public DbSet<AdminOtpAuditLog> AdminOtpAuditLogs => Set<AdminOtpAuditLog>();

    /// <summary>
    /// Gets or sets the platform settings categories table.
    /// </summary>
    public DbSet<PlatformSettingsCategory> PlatformSettingsCategories => Set<PlatformSettingsCategory>();

    /// <summary>
    /// Gets or sets the platform settings audit logs table.
    /// </summary>
    public DbSet<PlatformSettingsAuditLog> PlatformSettingsAuditLogs => Set<PlatformSettingsAuditLog>();

    /// <summary>
    /// Gets or sets the user roles join table.
    /// </summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>
    /// Gets or sets the Security Center blocked emails/domains table.
    /// </summary>
    public DbSet<BlockedEmail> BlockedEmails => Set<BlockedEmail>();

    /// <summary>
    /// Gets or sets the Security Center blocked IP addresses/ranges table.
    /// </summary>
    public DbSet<BlockedIp> BlockedIps => Set<BlockedIp>();

    /// <summary>
    /// Gets or sets the Security Center country restrictions table.
    /// </summary>
    public DbSet<CountryRestriction> CountryRestrictions => Set<CountryRestriction>();

    /// <summary>
    /// Gets or sets the Security Center blocked devices table (architecture-only, no live usage).
    /// </summary>
    public DbSet<BlockedDevice> BlockedDevices => Set<BlockedDevice>();

    /// <summary>
    /// Gets or sets the Security Center security event log table.
    /// </summary>
    public DbSet<SecurityEventLog> SecurityEventLogs => Set<SecurityEventLog>();

    /// <summary>
    /// Gets or sets the trusted/recognized devices table.
    /// </summary>
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters for soft-deletable entities.
    /// </summary>
    private static void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var condition = System.Linq.Expressions.Expression.Equal(
                property,
                System.Linq.Expressions.Expression.Constant(false));
            var lambda = System.Linq.Expressions.Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
