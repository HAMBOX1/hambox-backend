using HAMBOX.Modules.Identity.Domain.Permissions;
using HAMBOX.Modules.Identity.Domain.Roles;
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
    /// Gets the permissions database set.
    /// </summary>
    DbSet<Permission> Permissions { get; }

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
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous save operation, containing the number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
