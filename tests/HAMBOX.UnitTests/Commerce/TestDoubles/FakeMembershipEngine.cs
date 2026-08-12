using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Memberships.Models;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

internal sealed class FakeMembershipEngine : IMembershipEngine
{
    public Task<MembershipSnapshot> ResolveAsync(string? userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(MembershipSnapshot.None);

    public Task ProcessExpirationsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
