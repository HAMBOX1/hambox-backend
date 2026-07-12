using HAMBOX.Modules.Commerce.Application.Memberships.Models;

namespace HAMBOX.Modules.Commerce.Application.Memberships;

/// <summary>
/// Resolves membership plans, benefits, and active subscriptions.
/// </summary>
public interface IMembershipEngine
{
    Task<MembershipSnapshot> ResolveAsync(string? userId, CancellationToken cancellationToken = default);

    Task ProcessExpirationsAsync(CancellationToken cancellationToken = default);
}
