using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Themes;

/// <summary>
/// Aggregate root for a storefront theme definition.
/// </summary>
public sealed class StoreTheme : AggregateRoot, ISoftDeletable
{
    private readonly List<ThemeVersion> _versions = [];
    private readonly List<ThemeSchedule> _schedules = [];
    private readonly List<ThemeAssignment> _assignments = [];
    private readonly List<ThemeAsset> _assets = [];

    private StoreTheme()
    {
    }

    private StoreTheme(
        Guid id,
        string name,
        string slug,
        string? description,
        ThemeBaseMode baseMode,
        bool isDefault,
        bool isTemplate)
        : base(id)
    {
        Name = name;
        Slug = slug;
        Description = description;
        BaseMode = baseMode;
        Status = StoreThemeStatus.Draft;
        IsDefault = isDefault;
        IsTemplate = isTemplate;
        IsDeleted = false;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public StoreThemeStatus Status { get; private set; }
    public ThemeBaseMode BaseMode { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsTemplate { get; private set; }
    public Guid? PublishedVersionId { get; private set; }
    public int VersionCounter { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedOnUtc { get; set; }

    public IReadOnlyCollection<ThemeVersion> Versions => _versions.AsReadOnly();
    public IReadOnlyCollection<ThemeSchedule> Schedules => _schedules.AsReadOnly();
    public IReadOnlyCollection<ThemeAssignment> Assignments => _assignments.AsReadOnly();
    public IReadOnlyCollection<ThemeAsset> Assets => _assets.AsReadOnly();

    public static StoreTheme Create(string name, string slug, string? description, ThemeBaseMode baseMode, bool isDefault = false, bool isTemplate = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new StoreTheme(Guid.NewGuid(), name.Trim(), NormalizeSlug(slug), description?.Trim(), baseMode, isDefault, isTemplate);
    }

    public ThemeVersion CreateDraftVersion(Dictionary<string, string> tokens, string? notes = null)
    {
        VersionCounter++;
        var version = ThemeVersion.CreateDraft(Id, VersionCounter, tokens, notes);
        _versions.Add(version);
        return version;
    }

    public ThemeVersion PublishVersion(Guid versionId)
    {
        var version = _versions.SingleOrDefault(v => v.Id == versionId)
            ?? throw new InvalidOperationException("Theme version not found.");

        foreach (var existing in _versions.Where(v => v.IsPublished))
        {
            existing.Unpublish();
        }

        version.Publish();
        PublishedVersionId = version.Id;
        Status = StoreThemeStatus.Published;
        return version;
    }

    public void Archive()
    {
        if (IsDefault)
        {
            throw new InvalidOperationException("Default theme cannot be archived.");
        }

        Status = StoreThemeStatus.Archived;
    }

    public void Activate()
    {
        if (Status == StoreThemeStatus.Archived)
        {
            Status = PublishedVersionId.HasValue ? StoreThemeStatus.Published : StoreThemeStatus.Draft;
        }
    }

    public void Rename(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description?.Trim();
    }

    public ThemeSchedule AddSchedule(DateTime startsAtUtc, DateTime? endsAtUtc, string? recurrenceRule = null, int priority = 0)
    {
        var schedule = ThemeSchedule.Create(Id, startsAtUtc, endsAtUtc, recurrenceRule, priority);
        _schedules.Add(schedule);
        return schedule;
    }

    public ThemeAssignment AddAssignment(ThemeAssignmentType type, string targetKey, int priority = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKey);
        var assignment = ThemeAssignment.Create(Id, type, targetKey.Trim(), priority);
        _assignments.Add(assignment);
        return assignment;
    }

    public ThemeAsset UpsertAsset(ThemeAssetType assetType, string url, string? altText = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var existing = _assets.FirstOrDefault(a => a.AssetType == assetType);
        if (existing is not null)
        {
            existing.Update(url.Trim(), altText?.Trim());
            return existing;
        }

        var asset = ThemeAsset.Create(Id, assetType, url.Trim(), altText?.Trim());
        _assets.Add(asset);
        return asset;
    }

    public StoreTheme Duplicate(string newName, string newSlug)
    {
        var clone = Create(newName, newSlug, Description, BaseMode);
        var latest = _versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (latest is not null)
        {
            clone.CreateDraftVersion(latest.CloneTokens(), $"Cloned from {Name} v{latest.VersionNumber}");
        }

        foreach (var asset in _assets)
        {
            clone.UpsertAsset(asset.AssetType, asset.Url, asset.AltText);
        }

        return clone;
    }

    private static string NormalizeSlug(string slug) =>
        slug.Trim().ToLowerInvariant().Replace(' ', '-');
}
