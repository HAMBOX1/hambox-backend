namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// Blocks a single IP address or a CIDR range (IPv4 or IPv6). Matching is performed with
/// <see cref="System.Net.IPNetwork"/> in <see cref="Infrastructure.Services.ISecurityBlocklistService"/>.
/// </summary>
public sealed class BlockedIp : BlockListEntry
{
    private BlockedIp()
    {
    }

    private BlockedIp(Guid id, string cidrOrAddress, string reason, string? notes, DateTimeOffset? expiresOnUtc)
        : base(id, reason, notes, expiresOnUtc)
    {
        CidrOrAddress = cidrOrAddress;
    }

    /// <summary>
    /// Gets the blocked value: a single IP address (e.g. <c>203.0.113.5</c>) or a CIDR range
    /// (e.g. <c>203.0.113.0/24</c>).
    /// </summary>
    public string CidrOrAddress { get; private set; } = string.Empty;

    public static BlockedIp Create(string cidrOrAddress, string reason, string? notes = null, DateTimeOffset? expiresOnUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cidrOrAddress);

        var normalized = cidrOrAddress.Trim();
        if (!System.Net.IPNetwork.TryParse(normalized.Contains('/') ? normalized : normalized + "/32", out _)
            && !System.Net.IPNetwork.TryParse(normalized.Contains('/') ? normalized : normalized + "/128", out _))
        {
            throw new ArgumentException("Value must be a valid IP address or CIDR range.", nameof(cidrOrAddress));
        }

        return new BlockedIp(Guid.NewGuid(), normalized, reason, notes, expiresOnUtc);
    }
}
