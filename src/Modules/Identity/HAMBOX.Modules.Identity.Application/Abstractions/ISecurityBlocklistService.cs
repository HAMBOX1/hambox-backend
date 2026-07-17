using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Point-lookup surface over the Security Center blocklists (emails/domains, IPs/CIDR ranges,
/// countries). Backed by a short-lived in-memory cache so hot paths (login, every request) don't
/// hit the database on every call — see the Infrastructure implementation for cache details.
/// </summary>
public interface ISecurityBlocklistService
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="email"/> matches an active
    /// <c>BlockedEmail</c> entry, either exactly or via a <c>*@domain.com</c> wildcard.
    /// </summary>
    Task<bool> IsEmailBlockedAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="ipAddress"/> falls within an active
    /// <c>BlockedIp</c> entry (single address or CIDR range).
    /// </summary>
    Task<bool> IsIpBlockedAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the administrator-configured status for <paramref name="countryCode"/> (ISO-3166
    /// alpha-2). Countries with no override row return <see cref="CountryRestrictionStatus.Allowed"/>.
    /// </summary>
    Task<CountryRestrictionStatus> GetCountryStatusAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached blocklist state. Call after any write to a blocklist entity.
    /// </summary>
    void InvalidateCache();
}
