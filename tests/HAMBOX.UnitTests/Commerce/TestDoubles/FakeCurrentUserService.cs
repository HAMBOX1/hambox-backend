using HAMBOX.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

internal sealed class FakeCurrentUserService(string? userId, string? displayName = null) : ICurrentUserService
{
    public string? UserId { get; } = userId;

    public string? DisplayName { get; } = displayName;

    public bool IsAuthenticated => UserId is not null;

    public bool IsAdminContext => UserId is not null;
}
