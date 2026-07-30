using System.Text.Json;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;

namespace HAMBOX.Modules.Content.Application.Services;

/// <summary>
/// Centralizes (de)serialization of a template's section arrangement to/from the JSON blob stored on
/// <c>LandingPageTemplate.SectionsJson</c>/<c>DraftSectionsJson</c>. Public (not internal) because
/// Infrastructure's <c>LandingPageDataSeeder</c> needs it too.
/// </summary>
public static class LandingPageSectionsSerializer
{
    /// <summary>
    /// The exact options used to (de)serialize both the section list and each section's
    /// <c>configJson</c> payload — public so callers that need to serialize a single section's
    /// config (e.g. <c>LandingPageDataSeeder</c>'s backfill pass) match this module's casing/
    /// defaults exactly instead of constructing a second, possibly-divergent options instance.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyList<LandingPageSectionEntry> sections) =>
        JsonSerializer.Serialize(sections, Options);

    public static IReadOnlyList<LandingPageSectionEntry> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<LandingPageSectionEntry>>(json, Options) ?? [];
    }
}
