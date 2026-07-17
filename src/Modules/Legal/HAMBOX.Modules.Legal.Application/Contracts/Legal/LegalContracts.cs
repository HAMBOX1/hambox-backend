namespace HAMBOX.Modules.Legal.Application.Contracts.Legal;

public sealed record LegalSectionListItemDto(
    Guid Id,
    string Slug,
    string TitleEn,
    string? Category,
    string? Icon,
    int SortOrder,
    bool ShowInFooter,
    bool ShowInNavigation,
    bool RequireAcceptance,
    bool IsArchived,
    bool HasPublishedVersion,
    int? PublishedVersionNumber,
    DateTime? PublishedOnUtc,
    bool HasDraft,
    int VersionCount);

public sealed record LegalSectionDetailDto(
    Guid Id,
    string Slug,
    string? Category,
    string? Icon,
    int SortOrder,
    string? DescriptionEn,
    string? DescriptionAr,
    string? SeoTitle,
    string? SeoDescription,
    string? SeoKeywords,
    bool ShowInFooter,
    bool ShowInNavigation,
    bool RequireAcceptance,
    bool IsArchived,
    Guid? PublishedVersionId,
    LegalSectionVersionDto? PublishedVersion,
    LegalSectionVersionDto? DraftVersion,
    IReadOnlyList<LegalSectionVersionDto> Versions,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc,
    string? CreatedBy,
    string? ModifiedBy);

public sealed record LegalSectionVersionDto(
    Guid Id,
    int VersionNumber,
    string TitleEn,
    string? TitleAr,
    string ContentEn,
    string? ContentAr,
    string? VersionNotes,
    bool IsPublished,
    DateTime? PublishedOnUtc,
    string? PublishedBy);

public sealed record CreateLegalSectionRequest(
    string Slug,
    string TitleEn,
    string? TitleAr,
    string? Category,
    string? Icon,
    bool ShowInFooter,
    bool ShowInNavigation,
    bool RequireAcceptance);

public sealed record UpdateLegalSectionRequest(
    string Slug,
    string TitleEn,
    string? TitleAr,
    string ContentEn,
    string? ContentAr,
    string? Category,
    string? Icon,
    int SortOrder,
    string? DescriptionEn,
    string? DescriptionAr,
    string? SeoTitle,
    string? SeoDescription,
    string? SeoKeywords,
    bool ShowInFooter,
    bool ShowInNavigation,
    bool RequireAcceptance,
    string? VersionNotes);

public sealed record DuplicateLegalSectionRequest(string NewSlug, string? NewTitleEn);

public sealed record ArchiveLegalSectionRequest(bool Archived);

public sealed record LegalSectionHistoryEntryDto(
    Guid Id,
    string Action,
    string? ActorUserId,
    DateTimeOffset CreatedOnUtc,
    string? DetailsJson);

public sealed record LegalSectionQueryFilter(
    string? Search,
    string? Category,
    string? Status,
    bool? ShowInFooter,
    bool? ShowInNavigation,
    bool? RequireAcceptance);

public sealed record PublicLegalSectionSummaryDto(
    string Slug,
    string TitleEn,
    string? TitleAr,
    string? DescriptionEn,
    string? DescriptionAr,
    string? Category,
    string? Icon,
    int SortOrder,
    bool ShowInFooter,
    bool ShowInNavigation,
    bool RequireAcceptance,
    int VersionNumber,
    DateTime? PublishedOnUtc);

public sealed record PublicLegalSectionDto(
    string Slug,
    string TitleEn,
    string? TitleAr,
    string ContentEn,
    string? ContentAr,
    string? SeoTitle,
    string? SeoDescription,
    string? SeoKeywords,
    int VersionNumber,
    DateTime? PublishedOnUtc);

public sealed record LegalAcceptanceStatusDto(
    bool RequiresAcceptance,
    IReadOnlyList<string> StaleSlugs);
