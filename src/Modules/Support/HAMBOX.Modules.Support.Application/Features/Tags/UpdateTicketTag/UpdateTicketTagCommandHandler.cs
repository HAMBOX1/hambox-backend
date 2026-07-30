using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tags.UpdateTicketTag;

internal sealed class UpdateTicketTagCommandHandler(ISupportDbContext dbContext) : IRequestHandler<UpdateTicketTagCommand, Result>
{
    public async Task<Result> Handle(UpdateTicketTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await dbContext.TicketTags.FirstOrDefaultAsync(t => t.Id == request.TagId, cancellationToken);
        if (tag is null)
        {
            return Result.Failure(SupportErrors.TagNotFound);
        }

        tag.Update(request.Name, request.Color);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
