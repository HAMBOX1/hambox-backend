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

    public async Task<IReadOnlyDictionary<string, string>> FindUserIdsByEmailAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken = default)
    {
        var wanted = emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (wanted.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var users = await dbContext.Users.AsNoTracking()
            .Where(user => user.NormalizedEmail != null && wanted.Contains(user.NormalizedEmail))
            .Select(user => new { user.Id, user.Email })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Email.ToLowerInvariant(), u => u.Id.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<DateTimeOffset?> GetRegisteredOnAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return null;
        }

        return await dbContext.Users.AsNoTracking().Where(user => user.Id == id).Select(user => (DateTimeOffset?)user.CreatedOnUtc).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> GetEmailAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return null;
        }

        return await dbContext.Users.AsNoTracking().Where(user => user.Id == id).Select(user => user.Email).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> GetVerifiedEmailAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return null;
        }

        return await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == id && user.EmailConfirmed)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
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
            return new Dictionary<string, string>();
        }

        var users = await dbContext.Users.AsNoTracking()
            .Where(user => guids.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.Email })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            user => user.Id.ToString(),
            user =>
            {
                var name = $"{user.FirstName} {user.LastName}".Trim();
                return name.Length == 0 ? user.Email : $"{name} ({user.Email})";
            },
            StringComparer.OrdinalIgnoreCase);
    }
}
