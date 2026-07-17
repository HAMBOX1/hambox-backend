using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Communication.Domain.Communication;

public sealed class CommunicationTemplateVersion : BaseEntity
{
    private CommunicationTemplateVersion()
    {
    }

    private CommunicationTemplateVersion(
        Guid id,
        Guid templateId,
        int versionNumber,
        string subjectEn,
        string? subjectAr,
        string bodyEn,
        string? bodyAr,
        string? variablesJson)
        : base(id)
    {
        TemplateId = templateId;
        VersionNumber = versionNumber;
        SubjectEn = subjectEn;
        SubjectAr = subjectAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        VariablesJson = variablesJson;
        IsPublished = false;
    }

    public Guid TemplateId { get; private set; }
    public int VersionNumber { get; private set; }
    public string SubjectEn { get; private set; } = string.Empty;
    public string? SubjectAr { get; private set; }
    public string BodyEn { get; private set; } = string.Empty;
    public string? BodyAr { get; private set; }
    public string? VariablesJson { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTimeOffset? PublishedOnUtc { get; private set; }
    public string? PublishedBy { get; private set; }

    public static CommunicationTemplateVersion CreateDraft(
        Guid templateId,
        int versionNumber,
        string subjectEn,
        string? subjectAr,
        string bodyEn,
        string? bodyAr,
        string? variablesJson) =>
        new(Guid.NewGuid(), templateId, versionNumber, subjectEn, subjectAr, bodyEn, bodyAr, variablesJson);

    public void UpdateContent(string subjectEn, string? subjectAr, string bodyEn, string? bodyAr, string? variablesJson)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("Published template versions are immutable.");
        }

        SubjectEn = subjectEn;
        SubjectAr = subjectAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        VariablesJson = variablesJson;
    }

    public void Publish(string? publishedBy = null)
    {
        IsPublished = true;
        PublishedOnUtc = DateTimeOffset.UtcNow;
        PublishedBy = publishedBy;
    }

    public void Unpublish() => IsPublished = false;
}
