using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Services;

internal sealed record UserDisplaySummary(string Name, string Email);

/// <summary>Batch-resolves display name/email for a set of user ids, avoiding one query per row.</summary>
internal static class UserDisplayResolver
{
    public static async Task<Dictionary<string, UserDisplaySummary>> ResolveAsync(
        IIdentityDbContext identityDb, IEnumerable<string?> userIds, CancellationToken cancellationToken)
    {
        var guidIds = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Select(id => Guid.TryParse(id, out var guid) ? guid : (Guid?)null)
            .Where(guid => guid is not null)
            .Select(guid => guid!.Value)
            .ToList();

        if (guidIds.Count == 0)
        {
            return [];
        }

        var users = await identityDb.Users
            .AsNoTracking()
            .Where(u => guidIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            u => u.Id.ToString(),
            u => new UserDisplaySummary($"{u.FirstName} {u.LastName}".Trim(), u.Email));
    }

    public static async Task<UserDisplaySummary?> ResolveOneAsync(
        IIdentityDbContext identityDb, string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var guid))
        {
            return null;
        }

        var user = await identityDb.Users
            .AsNoTracking()
            .Where(u => u.Id == guid)
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? null : new UserDisplaySummary($"{user.FirstName} {user.LastName}".Trim(), user.Email);
    }
}
