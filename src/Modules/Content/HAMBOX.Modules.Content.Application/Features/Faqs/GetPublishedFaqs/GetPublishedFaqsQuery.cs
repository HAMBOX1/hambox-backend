using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.GetPublishedFaqs;

/// <summary>
/// Resolves the published FAQ set for a page: always Global, plus (when <paramref name="Scope"/> is
/// Product/Category) the FAQs scoped to <paramref name="TargetId"/>. Never returns unpublished/deleted
/// rows — the query filter is the enforcement point, not a UI-level filter (drafts must never leak
/// through this endpoint). Covers three call sites with one query: the <c>/faq</c> hub
/// (Scope=Global, TargetId=null), a product marketing page (Scope=Product, TargetId=productId), and a
/// category marketing page (Scope=Category, TargetId=categoryId).
/// </summary>
public sealed record GetPublishedFaqsQuery(FaqScope Scope = FaqScope.Global, Guid? TargetId = null)
    : IRequest<Result<IReadOnlyList<PublicFaqDto>>>;
