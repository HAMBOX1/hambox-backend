using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Communication.Domain.Communication;

/// <summary>
/// Aggregate root for a single notification template (e.g. "OrderConfirmation"). Versioning/immutability
/// mirrors <c>LegalDocument</c>/<c>LegalDocumentVersion</c> exactly — edits create a new draft version,
/// only the published version is ever rendered, and published versions can never be edited in place.
/// </summary>
public sealed class CommunicationTemplate : AggregateRoot
{
    private readonly List<CommunicationTemplateVersion> _versions = [];

    private CommunicationTemplate()
    {
    }

    private CommunicationTemplate(Guid id, string key, string channel, CommunicationCategory category)
        : base(id)
    {
        Key = key;
        Channel = channel;
        Category = category;
    }

    public string Key { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public CommunicationCategory Category { get; private set; }
    public Guid? ActiveVersionId { get; private set; }
    public int VersionCounter { get; private set; }

    public IReadOnlyCollection<CommunicationTemplateVersion> Versions => _versions.AsReadOnly();

    public static CommunicationTemplate Create(string key, string channel, CommunicationCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        return new CommunicationTemplate(Guid.NewGuid(), key.Trim(), channel.Trim(), category);
    }

    public CommunicationTemplateVersion CreateDraftVersion(
        string subjectEn, string? subjectAr, string bodyEn, string? bodyAr, string? variablesJson)
    {
        VersionCounter++;
        var version = CommunicationTemplateVersion.CreateDraft(
            Id, VersionCounter, subjectEn, subjectAr, bodyEn, bodyAr, variablesJson);
        _versions.Add(version);
        return version;
    }

    public CommunicationTemplateVersion PublishVersion(Guid versionId, string? publishedBy)
    {
        var version = _versions.SingleOrDefault(v => v.Id == versionId)
            ?? throw new InvalidOperationException("Communication template version not found.");

        foreach (var existing in _versions.Where(v => v.IsPublished))
        {
            existing.Unpublish();
        }

        version.Publish(publishedBy);
        ActiveVersionId = version.Id;
        return version;
    }
}
