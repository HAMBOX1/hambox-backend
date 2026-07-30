using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.CreateTicketPriority;

internal sealed class CreateTicketPriorityCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<CreateTicketPriorityCommand, Result<TicketPriorityDto>>
{
    public async Task<Result<TicketPriorityDto>> Handle(CreateTicketPriorityCommand request, CancellationToken cancellationToken)
    {
        var priority = TicketPriority.Create(
            request.Name, request.Color, request.Level, request.SlaFirstResponseMinutes, request.SlaResolutionMinutes);
        dbContext.TicketPriorities.Add(priority);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(SupportMapper.ToDto(priority));
    }
}
