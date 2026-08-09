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

    private LandingPageTemplate(Guid id, string name, string slug, string sectionsJson, LandingPageScope scope, Guid? targetId)
        : base(id)
    {
        Name = name;
        Slug = slug;
        SectionsJson = sectionsJson;
        IsActive = false;
        Scope = scope;
        TargetId = targetId;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    /// <summary>What this template is a page for. <see cref="LandingPageScope.Homepage"/> when <see cref="TargetId"/> is null.</summary>
    public LandingPageScope Scope { get; private set; } = LandingPageScope.Homepage;

    /// <summary>The Catalog product/category id this template is scoped to. Null for Homepage templates.</summary>
    public Guid? TargetId { get; private set; }

    /// <summary>Optional page-specific SEO overrides. When unset, callers fall back to the target's own name/description.</summary>
    public string? SeoTitle { get; private set; }
    public string? SeoDescription { get; private set; }
    public string? SeoOgImageUrl { get; private set; }

    /// <summary>Draft SEO overrides — same draft/publish semantics as <see cref="DraftSectionsJson"/>, so an admin editing SEO never leaks to the public page before Publish.</summary>
    public string? DraftSeoTitle { get; private set; }
    public string? DraftSeoDescription { get; private set; }
    public string? DraftSeoOgImageUrl { get; private set; }

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

    public static LandingPageTemplate Create(
        string name,
        string? initialSectionsJson = null,
        LandingPageScope scope = LandingPageScope.Homepage,
        Guid? targetId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (scope == LandingPageScope.Homepage && targetId is not null)
        {
            throw new ArgumentException("Homepage templates cannot have a TargetId.", nameof(targetId));
        }
        if (scope != LandingPageScope.Homepage && targetId is null)
        {
            throw new ArgumentException("Product/Category templates require a TargetId.", nameof(targetId));
        }

        return new LandingPageTemplate(Guid.NewGuid(), name.Trim(), NormalizeSlug(name), initialSectionsJson ?? "[]", scope, targetId);
    }

    public void SaveDraft(string sectionsJson, string? seoTitle = null, string? seoDescription = null, string? seoOgImageUrl = null)
    {
        ArgumentNullException.ThrowIfNull(sectionsJson);
        DraftSectionsJson = sectionsJson;
        DraftSeoTitle = seoTitle;
        DraftSeoDescription = seoDescription;
        DraftSeoOgImageUrl = seoOgImageUrl;
    }

    public void PublishDraft()
    {
        if (DraftSectionsJson is null)
        {
            throw new InvalidOperationException("Template has no draft to publish.");
        }

        SectionsJson = DraftSectionsJson;
        SeoTitle = DraftSeoTitle;
        SeoDescription = DraftSeoDescription;
        SeoOgImageUrl = DraftSeoOgImageUrl;
        DraftSectionsJson = null;
        DraftSeoTitle = null;
        DraftSeoDescription = null;
        DraftSeoOgImageUrl = null;
    }

    public void DiscardDraft()
    {
        DraftSectionsJson = null;
        DraftSeoTitle = null;
        DraftSeoDescription = null;
        DraftSeoOgImageUrl = null;
    }

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
