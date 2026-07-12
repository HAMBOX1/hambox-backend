using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Seeds verified admin users for local development and catalog CRUD testing.
/// </summary>
internal sealed class DevAdminDataSeeder(
    IdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<DevAdminSeedSettings> options,
    ILogger<DevAdminDataSeeder> logger)
{
    /// <summary>
    /// Ensures configured development admin accounts exist.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        var accounts = settings.EnumerateAccounts().ToList();
        if (accounts.Count == 0)
        {
            logger.LogWarning("DevAdminSeed is enabled but no accounts are configured.");
            return;
        }

        foreach (var account in accounts)
        {
            await SeedAccountAsync(account, cancellationToken);
        }
    }

    private async Task SeedAccountAsync(DevAdminAccountSeed account, CancellationToken cancellationToken)
    {
        var normalizedEmail = account.Email.ToUpperInvariant();
        var role = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == account.Role, cancellationToken);

        if (role is null)
        {
            logger.LogWarning(
                "DevAdminSeed role {Role} was not found for {Email}. Skipping admin seed.",
                account.Role,
                account.Email);
            return;
        }

        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            await EnsureAdminAccessAsync(existingUser, role.Id, account, cancellationToken);
            return;
        }

        var passwordHash = passwordHasher.HashPassword(account.Password);
        var user = ApplicationUser.Create(
            account.Email,
            passwordHash,
            account.FirstName,
            account.LastName);

        user.ConfirmEmail();
        user.Activate();

        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(UserRole.Create(user.Id, role.Id));

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded development admin user {Email} with role {Role}.",
            account.Email,
            account.Role);
    }

    private async Task EnsureAdminAccessAsync(
        ApplicationUser user,
        Guid roleId,
        DevAdminAccountSeed account,
        CancellationToken cancellationToken)
    {
        var hasRole = await dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == roleId, cancellationToken);

        var passwordHash = passwordHasher.HashPassword(account.Password);
        var updated = false;

        if (!user.EmailConfirmed)
        {
            user.ConfirmEmail();
            updated = true;
        }

        if (user.Status != Domain.Enums.UserStatus.Active)
        {
            user.Activate();
            updated = true;
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, account.Password))
        {
            user.UpdatePasswordHash(passwordHash);
            updated = true;
        }

        if (user.LockoutEnd.HasValue || user.AccessFailedCount > 0)
        {
            user.ResetAccessFailedCount();
            updated = true;
        }

        if (!hasRole)
        {
            dbContext.UserRoles.Add(UserRole.Create(user.Id, roleId));
            updated = true;
        }

        if (!updated)
        {
            logger.LogDebug("Development admin user {Email} already configured. Skipping seed.", account.Email);
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Updated development admin user {Email} with role {Role}.",
            account.Email,
            account.Role);
    }
}
