namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Provides access to the current date and time.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current date and time in UTC.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
