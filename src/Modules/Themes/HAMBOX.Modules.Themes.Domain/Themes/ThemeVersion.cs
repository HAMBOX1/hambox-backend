using System.Text.Json;
using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Themes;

public sealed class ThemeVersion : BaseEntity
{
    private ThemeVersion()
    {
    }

    private ThemeVersion(Guid id, Guid themeId, int versionNumber, string tokensJson, string? notes)
        : base(id)
    {
        ThemeId = themeId;
        VersionNumber = versionNumber;
        TokensJson = tokensJson;
        Notes = notes;
        IsPublished = false;
    }

    public Guid ThemeId { get; private set; }
    public int VersionNumber { get; private set; }
    public string TokensJson { get; private set; } = "{}";
    public string? Notes { get; private set; }
    public bool IsPublished { get; private set; }

    /// <summary>
    /// Permanent record that this version was published at least once. Unlike <see cref="IsPublished"/>
    /// (which flips back to false the moment another version supersedes it), this never resets — it is
    /// the real immutability guard and the eligibility check for rollback, since a superseded version is
    /// still a legitimate rollback target even though it's no longer the live one.
    /// </summary>
    public bool HasEverBeenPublished { get; private set; }
    public DateTime? PublishedOnUtc { get; private set; }
    public string? PublishedBy { get; private set; }
    public string? ValidationWarningsJson { get; private set; }

    public static ThemeVersion CreateDraft(Guid themeId, int versionNumber, Dictionary<string, string> tokens, string? notes)
    {
        return new ThemeVersion(Guid.NewGuid(), themeId, versionNumber, SerializeTokens(tokens), notes);
    }

    public void UpdateTokens(Dictionary<string, string> tokens, string? notes = null)
    {
        if (HasEverBeenPublished)
        {
            throw new InvalidOperationException("Published versions are immutable.");
        }

        TokensJson = SerializeTokens(tokens);
        if (notes is not null)
        {
            Notes = notes;
        }
    }

    public void Publish(string? publishedBy = null)
    {
        IsPublished = true;
        HasEverBeenPublished = true;
        PublishedOnUtc = DateTime.UtcNow;
        PublishedBy = publishedBy;
    }

    public void Unpublish()
    {
        IsPublished = false;
    }

    public Dictionary<string, string> GetTokens() => DeserializeTokens(TokensJson);

    public Dictionary<string, string> CloneTokens() => new(GetTokens(), StringComparer.OrdinalIgnoreCase);

    public void SetValidationWarnings(IReadOnlyList<string> warnings)
    {
        ValidationWarningsJson = warnings.Count == 0 ? null : JsonSerializer.Serialize(warnings);
    }

    private static string SerializeTokens(Dictionary<string, string> tokens) =>
        JsonSerializer.Serialize(tokens);

    private static Dictionary<string, string> DeserializeTokens(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
