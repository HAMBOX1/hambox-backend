namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Provides information about the current authenticated user.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user identifier.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets a human-readable display name (the user's email) for the current user, suitable for
    /// denormalizing onto audit trails without a cross-module lookup.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether the current request has an authenticated user.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets a value indicating whether the current request is authenticated in the admin portal
    /// auth context (as opposed to the storefront/customer context). Endpoints shared between the
    /// public storefront and the admin dashboard (e.g. product reads) must check this before
    /// including admin-only fields (like "last edited by") in a response, to avoid leaking staff
    /// identities to anonymous visitors.
    /// </summary>
    bool IsAdminContext { get; }

    /// <summary>
    /// Gets the session identifier (<c>UserSession.Id</c>) carried on the current access token, or
    /// <see langword="null"/> if the token predates this claim or isn't present. Lets a "sign out
    /// everywhere" handler distinguish and preserve the caller's own current session.
    /// </summary>
    Guid? SessionId { get; }
}
