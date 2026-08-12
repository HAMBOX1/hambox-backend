using HAMBOX.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

internal sealed class FakeCurrentUserService(string? userId) : ICurrentUserService
{
    public string? UserId { get; } = userId;

    public bool IsAuthenticated => UserId is not null;
}
