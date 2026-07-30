using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.DeleteTicketPriority;

internal sealed class DeleteTicketPriorityCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<DeleteTicketPriorityCommand, Result>
{
    public async Task<Result> Handle(DeleteTicketPriorityCommand request, CancellationToken cancellationToken)
    {
        var priority = await dbContext.TicketPriorities.FirstOrDefaultAsync(p => p.Id == request.PriorityId, cancellationToken);
        if (priority is null)
        {
            return Result.Failure(SupportErrors.PriorityNotFound);
        }

        priority.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
