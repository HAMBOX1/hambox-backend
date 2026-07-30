using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.UpdateTicketPriority;

internal sealed class UpdateTicketPriorityCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<UpdateTicketPriorityCommand, Result>
{
    public async Task<Result> Handle(UpdateTicketPriorityCommand request, CancellationToken cancellationToken)
    {
        var priority = await dbContext.TicketPriorities.FirstOrDefaultAsync(p => p.Id == request.PriorityId, cancellationToken);
        if (priority is null)
        {
            return Result.Failure(SupportErrors.PriorityNotFound);
        }

        priority.Update(
            request.Name, request.Color, request.Level, request.SlaFirstResponseMinutes, request.SlaResolutionMinutes, request.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
