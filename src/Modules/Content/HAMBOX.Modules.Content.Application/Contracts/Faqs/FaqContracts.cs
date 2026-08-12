using HAMBOX.Modules.Content.Domain.Faqs;

namespace HAMBOX.Modules.Content.Application.Contracts.Faqs;

/// <summary>Admin-facing shape — used for both the paged list and the single-item detail/edit view.</summary>
public sealed record FaqDto(
    Guid Id,
    string QuestionEn,
    string? QuestionAr,
    string AnswerEn,
    string? AnswerAr,
    Guid CategoryId,
    string CategoryNameEn,
    FaqScope Scope,
    Guid? TargetId,
    string? TargetLabel,
    int SortOrder,
    bool IsPublished,
    DateTime? PublishedOnUtc,
    DateTime ModifiedOnUtc);

public sealed record FaqCategoryDto(Guid Id, string NameEn, string? NameAr, string Slug, int SortOrder);

/// <summary>Public/storefront shape — never carries unpublished/deleted rows or admin-only audit fields.</summary>
public sealed record PublicFaqDto(
    Guid Id,
    string QuestionEn,
    string? QuestionAr,
    string AnswerEn,
    string? AnswerAr,
    Guid CategoryId,
    string CategoryNameEn,
    string? CategoryNameAr,
    FaqScope Scope,
    int SortOrder);

public sealed record FaqReorderEntry(Guid Id, int SortOrder);
