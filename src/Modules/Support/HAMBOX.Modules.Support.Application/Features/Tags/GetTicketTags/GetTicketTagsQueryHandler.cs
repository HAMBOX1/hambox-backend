using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tags.GetTicketTags;

internal sealed class GetTicketTagsQueryHandler(ISupportDbContext dbContext)
    : IRequestHandler<GetTicketTagsQuery, Result<IReadOnlyList<TicketTagDto>>>
{
    public async Task<Result<IReadOnlyList<TicketTagDto>>> Handle(GetTicketTagsQuery request, CancellationToken cancellationToken)
    {
        var tags = await dbContext.TicketTags.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<TicketTagDto>>(tags.Select(SupportMapper.ToDto).ToList());
    }
}
