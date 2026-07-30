using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.UpdateSavedReply;

internal sealed class UpdateSavedReplyCommandHandler(ISupportDbContext dbContext) : IRequestHandler<UpdateSavedReplyCommand, Result>
{
    public async Task<Result> Handle(UpdateSavedReplyCommand request, CancellationToken cancellationToken)
    {
        var reply = await dbContext.SavedReplies.FirstOrDefaultAsync(r => r.Id == request.ReplyId, cancellationToken);
        if (reply is null)
        {
            return Result.Failure(SupportErrors.SavedReplyNotFound);
        }

        reply.Update(request.FolderId, request.Title, request.Body);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
