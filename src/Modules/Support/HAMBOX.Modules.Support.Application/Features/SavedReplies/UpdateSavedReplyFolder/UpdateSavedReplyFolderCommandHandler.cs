using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.UpdateSavedReplyFolder;

internal sealed class UpdateSavedReplyFolderCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<UpdateSavedReplyFolderCommand, Result>
{
    public async Task<Result> Handle(UpdateSavedReplyFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await dbContext.SavedReplyFolders.FirstOrDefaultAsync(f => f.Id == request.FolderId, cancellationToken);
        if (folder is null)
        {
            return Result.Failure(SupportErrors.SavedReplyFolderNotFound);
        }

        folder.Update(request.Name, request.SortOrder);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
