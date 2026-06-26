using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Localization;

/// <inheritdoc />
internal sealed class UserLanguagePreferenceResolver(
    ICurrentUserService currentUserService,
    IIdentityDbContext dbContext) : IUserLanguagePreferenceResolver
{
    /// <inheritdoc />
    public async Task<string?> GetPreferredLanguageAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return null;
        }

        if (!Guid.TryParse(currentUserService.UserId, out var userId))
        {
            return null;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
