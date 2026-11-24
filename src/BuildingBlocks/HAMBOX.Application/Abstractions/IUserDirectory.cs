namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Answers "which of these user ids belong to real accounts?" for modules that store a user id as a plain string
/// and have no Identity access of their own (e.g. Commerce memberships), so they can refuse an id that matches nobody.
/// </summary>
public interface IUserDirectory
{
    /// <summary>Returns the subset of <paramref name="userIds"/> that exist, in the canonical string form stored elsewhere.</summary>
    Task<IReadOnlySet<string>> GetExistingUserIdsAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>Maps each known e-mail (lower-cased) to its user id; unknown addresses are simply absent.</summary>
    Task<IReadOnlyDictionary<string, string>> FindUserIdsByEmailAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default);

    /// <summary>When the account was created (for anniversary campaigns); null for an unknown user.</summary>
    Task<DateTimeOffset?> GetRegisteredOnAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>The user's e-mail address regardless of verification (for staff-created records such as B2B orders).</summary>
    Task<string?> GetEmailAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>The user's e-mail address, but only when they have verified it; null otherwise (used to link an imported archive record safely).</summary>
    Task<string?> GetVerifiedEmailAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>A readable label ("Ann Smith (ann@example.com)") for each known user id, for showing who did something in an admin screen.</summary>
    Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);
}
