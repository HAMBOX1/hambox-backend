namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Answers "which of these user ids belong to real accounts?" for modules that store a user id as a plain string
/// and have no Identity access of their own (e.g. Commerce memberships), so they can refuse an id that matches nobody.
/// </summary>
public interface IUserDirectory
{
    /// <summary>Returns the subset of <paramref name="userIds"/> that exist, in the canonical string form stored elsewhere.</summary>
    Task<IReadOnlySet<string>> GetExistingUserIdsAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);
}
