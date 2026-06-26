namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Resolves the preferred UI culture for the authenticated user.
/// </summary>
public interface IUserLanguagePreferenceResolver
{
    /// <summary>
    /// Returns a supported culture code (e.g. en, ar) or <see langword="null"/>.
    /// </summary>
    Task<string?> GetPreferredLanguageAsync(CancellationToken cancellationToken = default);
}
