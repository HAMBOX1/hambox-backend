using HAMBOX.Modules.Legal.Application.Contracts.Legal;
using HAMBOX.Modules.Legal.Domain.Legal;

namespace HAMBOX.Modules.Legal.Application.Services;

public static class LegalMapper
{
    public static LegalSectionVersionDto ToVersionDto(LegalSectionVersion version) => new(
        version.Id,
        version.VersionNumber,
        version.TitleEn,
        version.TitleAr,
        version.ContentEn,
        version.ContentAr,
        version.VersionNotes,
        version.IsPublished,
        version.PublishedOnUtc,
        version.PublishedBy);

    public static LegalSectionListItemDto ToListItem(LegalSection section)
    {
        var published = section.PublishedVersionId.HasValue
            ? section.Versions.FirstOrDefault(v => v.Id == section.PublishedVersionId)
            : null;
        var draft = CurrentDraft(section);
        var titleEn = published?.TitleEn ?? draft?.TitleEn ?? section.Slug;

        return new LegalSectionListItemDto(
            section.Id,
            section.Slug,
            titleEn,
            section.Category,
            section.Icon,
            section.SortOrder,
            section.ShowInFooter,
            section.ShowInNavigation,
            section.RequireAcceptance,
            section.IsArchived,
            published is not null,
            published?.VersionNumber,
            published?.PublishedOnUtc,
            draft is not null,
            section.Versions.Count);
    }

    public static LegalSectionDetailDto ToDetail(LegalSection section)
    {
        var published = section.PublishedVersionId.HasValue
            ? section.Versions.FirstOrDefault(v => v.Id == section.PublishedVersionId)
            : null;
        var draft = CurrentDraft(section);

        return new LegalSectionDetailDto(
            section.Id,
            section.Slug,
            section.Category,
            section.Icon,
            section.SortOrder,
            section.DescriptionEn,
            section.DescriptionAr,
            section.SeoTitle,
            section.SeoDescription,
            section.SeoKeywords,
            section.ShowInFooter,
            section.ShowInNavigation,
            section.RequireAcceptance,
            section.IsArchived,
            section.PublishedVersionId,
            published is null ? null : ToVersionDto(published),
            draft is null ? null : ToVersionDto(draft),
            section.Versions.OrderByDescending(v => v.VersionNumber).Select(ToVersionDto).ToList(),
            section.CreatedOnUtc,
            section.ModifiedOnUtc,
            section.CreatedBy,
            section.ModifiedBy);
    }

    public static PublicLegalSectionSummaryDto ToPublicSummary(LegalSection section, LegalSectionVersion published) => new(
        section.Slug,
        published.TitleEn,
        published.TitleAr,
        section.DescriptionEn,
        section.DescriptionAr,
        section.Category,
        section.Icon,
        section.SortOrder,
        section.ShowInFooter,
        section.ShowInNavigation,
        section.RequireAcceptance,
        published.VersionNumber,
        published.PublishedOnUtc);

    public static PublicLegalSectionDto ToPublicDetail(LegalSection section, LegalSectionVersion published) => new(
        section.Slug,
        published.TitleEn,
        published.TitleAr,
        published.ContentEn,
        published.ContentAr,
        section.SeoTitle,
        section.SeoDescription,
        section.SeoKeywords,
        published.VersionNumber,
        published.PublishedOnUtc);

    private static LegalSectionVersion? CurrentDraft(LegalSection section) => section.GetCurrentDraft();
}
