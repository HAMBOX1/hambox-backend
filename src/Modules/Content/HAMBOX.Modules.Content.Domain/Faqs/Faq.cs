using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Content.Domain.Faqs;

/// <summary>
/// A single question/answer entry. Scoped via <see cref="Scope"/>/<see cref="TargetId"/> exactly like
/// <c>LandingPageTemplate</c>: <see cref="FaqScope.Global"/> has no target, <see cref="FaqScope.Product"/>/
/// <see cref="FaqScope.Category"/> require one. Publish/unpublish is an instant toggle (no draft/publish
/// split — there is no admin-preview workflow to protect here, unlike landing page sections).
/// </summary>
public sealed class Faq : AggregateRoot, IAuditable, ISoftDeletable
{
    private Faq()
    {
    }

    private Faq(
        Guid id,
        string questionEn,
        string? questionAr,
        string answerEn,
        string? answerAr,
        Guid categoryId,
        FaqScope scope,
        Guid? targetId,
        int sortOrder)
        : base(id)
    {
        QuestionEn = questionEn;
        QuestionAr = questionAr;
        AnswerEn = answerEn;
        AnswerAr = answerAr;
        CategoryId = categoryId;
        Scope = scope;
        TargetId = targetId;
        SortOrder = sortOrder;
        IsPublished = false;
    }

    public string QuestionEn { get; private set; } = string.Empty;
    public string? QuestionAr { get; private set; }
    public string AnswerEn { get; private set; } = string.Empty;
    public string? AnswerAr { get; private set; }
    public Guid CategoryId { get; private set; }
    public FaqScope Scope { get; private set; }
    public Guid? TargetId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTimeOffset? PublishedOnUtc { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static Faq Create(
        string questionEn,
        string? questionAr,
        string answerEn,
        string? answerAr,
        Guid categoryId,
        FaqScope scope,
        Guid? targetId,
        int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(answerEn);
        ValidateTarget(scope, targetId);

        return new Faq(
            Guid.NewGuid(),
            questionEn.Trim(),
            NormalizeOptional(questionAr),
            answerEn.Trim(),
            NormalizeOptional(answerAr),
            categoryId,
            scope,
            targetId,
            sortOrder);
    }

    public void Update(
        string questionEn,
        string? questionAr,
        string answerEn,
        string? answerAr,
        Guid categoryId,
        FaqScope scope,
        Guid? targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(answerEn);
        ValidateTarget(scope, targetId);

        QuestionEn = questionEn.Trim();
        QuestionAr = NormalizeOptional(questionAr);
        AnswerEn = answerEn.Trim();
        AnswerAr = NormalizeOptional(answerAr);
        CategoryId = categoryId;
        Scope = scope;
        TargetId = targetId;
    }

    public void Publish()
    {
        IsPublished = true;
        PublishedOnUtc = DateTimeOffset.UtcNow;
    }

    public void Unpublish() => IsPublished = false;

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    /// <summary>Returns a new, unpublished FAQ with the same content — the caller assigns it a fresh SortOrder/Id.</summary>
    public Faq Duplicate() =>
        new(
            Guid.NewGuid(),
            $"{QuestionEn} (Copy)",
            QuestionAr,
            AnswerEn,
            AnswerAr,
            CategoryId,
            Scope,
            TargetId,
            SortOrder);

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }

    private static void ValidateTarget(FaqScope scope, Guid? targetId)
    {
        if (scope == FaqScope.Global && targetId is not null)
        {
            throw new ArgumentException("Global FAQs cannot have a TargetId.", nameof(targetId));
        }

        if (scope != FaqScope.Global && targetId is null)
        {
            throw new ArgumentException("Product/Category FAQs require a TargetId.", nameof(targetId));
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
