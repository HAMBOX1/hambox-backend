namespace HAMBOX.SharedKernel.Errors;

/// <summary>
/// Represents an application error.
/// </summary>
/// <param name="Code">The stable error code.</param>
/// <param name="Description">The human-readable error description.</param>
public sealed record Error(string Code, string Description)
{
    /// <summary>
    /// Represents the absence of an error.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>
    /// Represents an error caused by an unexpected null value.
    /// </summary>
    public static readonly Error NullValue = new(
        "Error.NullValue",
        "The specified result value is null.");
}
