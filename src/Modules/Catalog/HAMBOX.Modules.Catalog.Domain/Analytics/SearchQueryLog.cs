using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Analytics;

/// <summary>
/// Best-effort log of a catalog/product search query for analytics.
/// </summary>
public sealed class SearchQueryLog : Entity
{
    private SearchQueryLog()
    {
    }

    private SearchQueryLog(
        Guid id,
        string query,
        int resultCount,
        string? userId,
        string? ip)
        : base(id)
    {
        Query = query;
        ResultCount = resultCount;
        UserId = userId;
        Ip = ip;
        CreatedOnUtc = DateTimeOffset.UtcNow;
    }

    public string Query { get; private set; } = string.Empty;
    public int ResultCount { get; private set; }
    public string? UserId { get; private set; }
    public string? Ip { get; private set; }

    public static SearchQueryLog Create(
        string query,
        int resultCount,
        string? userId = null,
        string? ip = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return new SearchQueryLog(
            Guid.NewGuid(),
            query.Trim(),
            Math.Max(0, resultCount),
            string.IsNullOrWhiteSpace(userId) ? null : userId.Trim(),
            string.IsNullOrWhiteSpace(ip) ? null : ip.Trim());
    }
}
