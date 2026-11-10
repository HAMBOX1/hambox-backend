using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class UserDirectory(IIdentityDbContext dbContext) : IUserDirectory
{
    public async Task<IReadOnlySet<string>> GetExistingUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var guids = userIds
            .Select(id => Guid.TryParse(id, out var guid) ? guid : (Guid?)null)
            .Where(guid => guid.HasValue)
            .Select(guid => guid!.Value)
            .Distinct()
            .ToList();

        if (guids.Count == 0)
        {
            return new HashSet<string>();
        }

        var found = await dbContext.Users.AsNoTracking()
            .Where(user => guids.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        return found.Select(id => id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
