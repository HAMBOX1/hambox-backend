using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.DeleteSavedReply;

internal sealed class DeleteSavedReplyCommandHandler(ISupportDbContext dbContext) : IRequestHandler<DeleteSavedReplyCommand, Result>
{
    public async Task<Result> Handle(DeleteSavedReplyCommand request, CancellationToken cancellationToken)
    {
        var reply = await dbContext.SavedReplies.FirstOrDefaultAsync(r => r.Id == request.ReplyId, cancellationToken);
        if (reply is null)
        {
            return Result.Failure(SupportErrors.SavedReplyNotFound);
        }

        reply.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
