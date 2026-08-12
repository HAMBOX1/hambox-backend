using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.GetPublishedFaqs;

internal sealed class GetPublishedFaqsQueryHandler(IContentDbContext dbContext)
    : IRequestHandler<GetPublishedFaqsQuery, Result<IReadOnlyList<PublicFaqDto>>>
{
    public async Task<Result<IReadOnlyList<PublicFaqDto>>> Handle(GetPublishedFaqsQuery request, CancellationToken cancellationToken)
    {
        var dtos = await (
            from f in dbContext.Faqs.AsNoTracking()
            join c in dbContext.FaqCategories.AsNoTracking() on f.CategoryId equals c.Id
            where f.IsPublished
                && (f.Scope == FaqScope.Global || (f.Scope == request.Scope && f.TargetId == request.TargetId))
            orderby f.SortOrder
            select new PublicFaqDto(
                f.Id,
                f.QuestionEn,
                f.QuestionAr,
                f.AnswerEn,
                f.AnswerAr,
                f.CategoryId,
                c.NameEn,
                c.NameAr,
                f.Scope,
                f.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PublicFaqDto>>(dtos);
    }
}
