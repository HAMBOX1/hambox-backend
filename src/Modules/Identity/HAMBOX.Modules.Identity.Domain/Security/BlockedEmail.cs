namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// Blocks a single email address (<c>user@example.com</c>) or an entire domain via a wildcard
/// pattern (<c>*@spamdomain.com</c>). Blocks registration, login, and password reset for any
/// matching address — see <see cref="Infrastructure.Services.ISecurityBlocklistService"/> for matching.
/// </summary>
public sealed class BlockedEmail : BlockListEntry
{
    private BlockedEmail()
    {
    }

    private BlockedEmail(Guid id, string pattern, string reason, string? notes, DateTimeOffset? expiresOnUtc)
        : base(id, reason, notes, expiresOnUtc)
    {
        Pattern = pattern;
    }

    /// <summary>
    /// Gets the blocked pattern: either an exact, normalized (lowercased) email address, or a
    /// wildcard domain pattern of the form <c>*@domain.com</c>.
    /// </summary>
    public string Pattern { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether <see cref="Pattern"/> is a wildcard domain pattern
    /// rather than a single exact email address.
    /// </summary>
    public bool IsWildcardDomain => Pattern.StartsWith("*@", StringComparison.Ordinal);

    public static BlockedEmail Create(string pattern, string reason, string? notes = null, DateTimeOffset? expiresOnUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var normalized = pattern.Trim().ToLowerInvariant();
        if (!normalized.Contains('@'))
        {
            throw new ArgumentException("Pattern must be an email address or a '*@domain.com' wildcard.", nameof(pattern));
        }

        return new BlockedEmail(Guid.NewGuid(), normalized, reason, notes, expiresOnUtc);
    }
}
