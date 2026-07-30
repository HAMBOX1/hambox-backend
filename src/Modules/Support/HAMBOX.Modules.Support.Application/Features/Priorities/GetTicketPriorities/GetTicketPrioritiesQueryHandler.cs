using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.GetTicketPriorities;

internal sealed class GetTicketPrioritiesQueryHandler(ISupportDbContext dbContext)
    : IRequestHandler<GetTicketPrioritiesQuery, Result<IReadOnlyList<TicketPriorityDto>>>
{
    public async Task<Result<IReadOnlyList<TicketPriorityDto>>> Handle(GetTicketPrioritiesQuery request, CancellationToken cancellationToken)
    {
        var priorities = await dbContext.TicketPriorities.AsNoTracking()
            .OrderBy(p => p.Level).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<TicketPriorityDto>>(priorities.Select(SupportMapper.ToDto).ToList());
    }
}
