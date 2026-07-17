using System.Net;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Point-lookup surface over the Security Center blocklists, cached in-memory for a short window
/// so hot paths (login, every request for IP checks) never hit the database directly — mirrors
/// <c>PlatformSettingsService</c>'s cache-with-manual-invalidation shape, just with a much shorter
/// TTL since these lists can be edited by an admin and are expected to take effect quickly.
/// </summary>
internal sealed class SecurityBlocklistService(IIdentityDbContext dbContext, IMemoryCache memoryCache) : ISecurityBlocklistService
{
    private const string EmailsCacheKey = "security-blocklist:emails";
    private const string IpsCacheKey = "security-blocklist:ips";
    private const string CountriesCacheKey = "security-blocklist:countries";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    public async Task<bool> IsEmailBlockedAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var atIndex = normalized.IndexOf('@');
        if (atIndex < 0)
        {
            return false;
        }

        var domain = normalized[(atIndex + 1)..];
        var patterns = await GetActiveEmailPatternsAsync(cancellationToken);

        return patterns.Contains(normalized) || patterns.Contains($"*@{domain}");
    }

    public async Task<bool> IsIpBlockedAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(ipAddress, out var address))
        {
            return false;
        }

        var ranges = await GetActiveIpRangesAsync(cancellationToken);

        return ranges.Any(range => IPNetwork.TryParse(range, out var network) && network.Contains(address));
    }

    public async Task<CountryRestrictionStatus> GetCountryStatusAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var overrides = await GetCountryOverridesAsync(cancellationToken);
        var code = countryCode.Trim().ToUpperInvariant();

        return overrides.TryGetValue(code, out var status) ? status : CountryRestrictionStatus.Allowed;
    }

    public void InvalidateCache()
    {
        memoryCache.Remove(EmailsCacheKey);
        memoryCache.Remove(IpsCacheKey);
        memoryCache.Remove(CountriesCacheKey);
    }

    private Task<HashSet<string>> GetActiveEmailPatternsAsync(CancellationToken cancellationToken) =>
        memoryCache.GetOrCreateAsync(EmailsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var now = DateTimeOffset.UtcNow;
            var patterns = await dbContext.BlockedEmails
                .AsNoTracking()
                .Where(b => b.ExpiresOnUtc == null || b.ExpiresOnUtc > now)
                .Select(b => b.Pattern)
                .ToListAsync(cancellationToken);
            return patterns.ToHashSet();
        })!;

    private Task<List<string>> GetActiveIpRangesAsync(CancellationToken cancellationToken) =>
        memoryCache.GetOrCreateAsync(IpsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var now = DateTimeOffset.UtcNow;
            return await dbContext.BlockedIps
                .AsNoTracking()
                .Where(b => b.ExpiresOnUtc == null || b.ExpiresOnUtc > now)
                .Select(b => b.CidrOrAddress.Contains('/') ? b.CidrOrAddress : b.CidrOrAddress + (b.CidrOrAddress.Contains(':') ? "/128" : "/32"))
                .ToListAsync(cancellationToken);
        })!;

    private Task<Dictionary<string, CountryRestrictionStatus>> GetCountryOverridesAsync(CancellationToken cancellationToken) =>
        memoryCache.GetOrCreateAsync(CountriesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var now = DateTimeOffset.UtcNow;
            return await dbContext.CountryRestrictions
                .AsNoTracking()
                .Where(c => c.ExpiresOnUtc == null || c.ExpiresOnUtc > now)
                .ToDictionaryAsync(c => c.CountryCode, c => c.Status, cancellationToken);
        })!;
}
