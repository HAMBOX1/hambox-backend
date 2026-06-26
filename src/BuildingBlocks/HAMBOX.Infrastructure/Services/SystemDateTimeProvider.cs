using HAMBOX.Application.Abstractions;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// System implementation of IDateTimeProvider returning the system UtcNow.
/// Used for design-time operations and migrations.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
