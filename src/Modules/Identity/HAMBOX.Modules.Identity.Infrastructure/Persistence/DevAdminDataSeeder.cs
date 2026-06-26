using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Seeds a verified admin user for local development and catalog CRUD testing.
/// </summary>
internal sealed class DevAdminDataSeeder(
    IdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<DevAdminSeedSettings> options,
    ILogger<DevAdminDataSeeder> logger)
{
    /// <summary>
    /// Ensures the configured development admin account exists.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogWarning(
                "DevAdminSeed is enabled but Email or Password is missing. Skipping admin seed.");
            return;
        }

        var normalizedEmail = settings.Email.ToUpperInvariant();
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            logger.LogDebug("Development admin user {Email} already exists. Skipping seed.", settings.Email);
            return;
        }

        var role = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == settings.Role, cancellationToken);

        if (role is null)
        {
            logger.LogWarning(
                "DevAdminSeed role {Role} was not found. Skipping admin seed.",
                settings.Role);
            return;
        }

        var passwordHash = passwordHasher.HashPassword(settings.Password);
        var user = ApplicationUser.Create(
            settings.Email,
            passwordHash,
            settings.FirstName,
            settings.LastName);

        user.ConfirmEmail();
        user.Activate();

        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(UserRole.Create(user.Id, role.Id));

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded development admin user {Email} with role {Role}.",
            settings.Email,
            settings.Role);
    }
}
