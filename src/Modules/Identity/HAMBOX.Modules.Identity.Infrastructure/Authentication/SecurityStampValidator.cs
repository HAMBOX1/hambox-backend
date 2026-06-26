using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Validates JWT security stamps against the current user record in the database.
/// </summary>
internal sealed class SecurityStampValidator(IIdentityDbContext dbContext) : ISecurityStampValidator
{
    /// <inheritdoc />
    public async Task<bool> ValidateAsync(
        Guid userId,
        string securityStamp,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(securityStamp))
        {
            return false;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is not null
            && user.Status == UserStatus.Active
            && user.SecurityStamp == securityStamp;
    }
}
