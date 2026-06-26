using HAMBOX.Application.Abstractions;

namespace HAMBOX.Infrastructure.Services;

/// <summary>
/// Provides access to the current date and time in UTC.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
