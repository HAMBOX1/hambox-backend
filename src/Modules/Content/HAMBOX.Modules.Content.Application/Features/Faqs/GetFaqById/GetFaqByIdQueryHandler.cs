using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqById;

internal sealed class GetFaqByIdQueryHandler(IContentDbContext dbContext, ICatalogDbContext catalogDbContext)
    : IRequestHandler<GetFaqByIdQuery, Result<FaqDto>>
{
    public async Task<Result<FaqDto>> Handle(GetFaqByIdQuery request, CancellationToken cancellationToken)
    {
        var faq = await dbContext.Faqs.AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (faq is null)
        {
            return Result.Failure<FaqDto>(ContentErrors.FaqNotFound);
        }

        var dtos = await GetFaqsQueryHandler.MapToDtosAsync([faq], dbContext, catalogDbContext, cancellationToken);
        return Result.Success(dtos[0]);
    }
}
