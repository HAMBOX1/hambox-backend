using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Content.Domain.LandingPages;

/// <summary>
/// Aggregate root for a landing page (storefront homepage) template — a named, versionable
/// arrangement of sections with draft/publish semantics mirroring the Themes module's
/// <c>StoreTheme</c>/<c>ThemeVersion</c> draft workflow, collapsed onto a single JSON blob
/// per template rather than a separate child-version table (Phase 1 scope).
/// </summary>
public sealed class LandingPageTemplate : AggregateRoot, IAuditable, ISoftDeletable
{
    private LandingPageTemplate()
    {
    }

    private LandingPageTemplate(Guid id, string name, string slug, string sectionsJson)
        : base(id)
    {
        Name = name;
        Slug = slug;
        SectionsJson = sectionsJson;
        IsActive = false;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    /// <summary>The published, live section arrangement (JSON array of section entries).</summary>
    public string SectionsJson { get; private set; } = "[]";

    /// <summary>Unsaved edits not yet published. Null means there is no pending draft.</summary>
    public string? DraftSectionsJson { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>Gets a value indicating whether this template has draft edits not yet published.</summary>
    public bool HasUnpublishedChanges => DraftSectionsJson is not null;

    public static LandingPageTemplate Create(string name, string? initialSectionsJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new LandingPageTemplate(Guid.NewGuid(), name.Trim(), NormalizeSlug(name), initialSectionsJson ?? "[]");
    }

    public void SaveDraft(string sectionsJson)
    {
        ArgumentNullException.ThrowIfNull(sectionsJson);
        DraftSectionsJson = sectionsJson;
    }

    public void PublishDraft()
    {
        if (DraftSectionsJson is null)
        {
            throw new InvalidOperationException("Template has no draft to publish.");
        }

        SectionsJson = DraftSectionsJson;
        DraftSectionsJson = null;
    }

    public void DiscardDraft() => DraftSectionsJson = null;

    /// <summary>
    /// Replaces the published (and, when provided, draft) sections JSON in place without touching
    /// draft/publish state otherwise — used only by <c>LandingPageDataSeeder</c>'s one-time
    /// placeholder-<c>configJson</c> backfill, which patches individual section entries' config
    /// and must not be confused with an admin edit (<see cref="SaveDraft"/>/<see cref="PublishDraft"/>).
    /// Pass <paramref name="draftSectionsJson"/> as null to leave <see cref="DraftSectionsJson"/> untouched.
    /// </summary>
    public void BackfillSectionsJson(string sectionsJson, string? draftSectionsJson)
    {
        ArgumentNullException.ThrowIfNull(sectionsJson);
        SectionsJson = sectionsJson;
        if (draftSectionsJson is not null)
        {
            DraftSectionsJson = draftSectionsJson;
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizeSlug(string name) =>
        name.Trim().ToLowerInvariant().Replace(' ', '-');
}
