using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Categories.GetTicketCategories;

internal sealed class GetTicketCategoriesQueryHandler(ISupportDbContext dbContext)
    : IRequestHandler<GetTicketCategoriesQuery, Result<IReadOnlyList<TicketCategoryDto>>>
{
    public async Task<Result<IReadOnlyList<TicketCategoryDto>>> Handle(GetTicketCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await dbContext.TicketCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<TicketCategoryDto>>(categories.Select(SupportMapper.ToDto).ToList());
    }
}
