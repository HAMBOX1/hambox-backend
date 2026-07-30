using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tags.DeleteTicketTag;

internal sealed class DeleteTicketTagCommandHandler(ISupportDbContext dbContext) : IRequestHandler<DeleteTicketTagCommand, Result>
{
    public async Task<Result> Handle(DeleteTicketTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await dbContext.TicketTags.FirstOrDefaultAsync(t => t.Id == request.TagId, cancellationToken);
        if (tag is null)
        {
            return Result.Failure(SupportErrors.TagNotFound);
        }

        tag.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
