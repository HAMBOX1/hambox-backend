namespace HAMBOX.Modules.Content.Application.Contracts.LandingPages;

public sealed record LandingPageSectionEntry(
    Guid InstanceId,
    string Category,
    string VariantKey,
    int SortOrder,
    bool IsVisible,
    string ConfigJson);

public sealed record LandingPageTemplateSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    bool HasUnpublishedChanges,
    DateTime ModifiedOnUtc);

public sealed record LandingPageTemplateDetailDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    bool HasUnpublishedChanges,
    IReadOnlyList<LandingPageSectionEntry> Sections);

public sealed record PublishedLandingPageDto(
    Guid TemplateId,
    string TemplateName,
    IReadOnlyList<LandingPageSectionEntry> Sections,
    DateTime? PublishedOnUtc);

/// <summary>
/// Wraps the optional footer section so it can travel through <see cref="HAMBOX.SharedKernel.Results.Result{TValue}"/>,
/// whose <c>Success</c> factory throws on a null value — a bare <c>Result&lt;LandingPageSectionEntry?&gt;</c>
/// cannot represent "successfully found no footer section". See GetPublishedLandingPageFooterQuery.
/// </summary>
public sealed record LandingPageFooterDto(LandingPageSectionEntry? Section);
