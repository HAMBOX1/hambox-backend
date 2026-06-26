using HAMBOX.Application.Abstractions;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// A null implementation of ICurrentUserService that represents an unauthenticated state.
/// Used for design-time operations and migrations.
/// </summary>
public sealed class NullCurrentUserService : ICurrentUserService
{
    /// <inheritdoc />
    public string? UserId => null;

    /// <inheritdoc />
    public bool IsAuthenticated => false;
}
