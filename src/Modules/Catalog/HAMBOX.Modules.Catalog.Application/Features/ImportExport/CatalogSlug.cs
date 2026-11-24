using System.Text.RegularExpressions;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport;

/// <summary>The one way an import turns a typed name into a URL slug (lower-case letters, digits and dashes).</summary>
public static partial class CatalogSlug
{
    public static string From(string value)
    {
        var slug = NonSlugCharacters().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return slug.Length > 0 ? slug : Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>A slug not in <paramref name="taken"/>: the plain one, else with <paramref name="parentSlug"/> in front, else numbered. The result is added to <paramref name="taken"/>.</summary>
    public static string Unique(string name, string? parentSlug, ISet<string> taken)
    {
        var plain = From(name);
        var candidates = parentSlug is null ? [plain] : new[] { plain, $"{parentSlug}-{plain}" };
        foreach (var candidate in candidates)
        {
            if (taken.Add(candidate))
            {
                return candidate;
            }
        }

        for (var n = 2; ; n++)
        {
            var numbered = $"{candidates[^1]}-{n}";
            if (taken.Add(numbered))
            {
                return numbered;
            }
        }
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
