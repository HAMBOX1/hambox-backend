namespace HAMBOX.Modules.Catalog.Application.Services;

/// <summary>
/// Normalises a free-text search term before it reaches a query. EF parameterises <c>Contains</c> against the column
/// width, so an over-long term (for example a 5,000-character query string on an anonymous endpoint) makes SQL Server
/// throw "String or binary data would be truncated" and the request fails with a 500. No product, SKU or category
/// name is that long, so the term is simply capped.
/// </summary>
public static class SearchInput
{
    public const int MaxLength = 100;

    public static string? Clean(string? term)
    {
        var trimmed = term?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;
    }
}
