using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.SavedReplies;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.CreateSavedReplyFolder;

internal sealed class CreateSavedReplyFolderCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<CreateSavedReplyFolderCommand, Result<SavedReplyFolderDto>>
{
    public async Task<Result<SavedReplyFolderDto>> Handle(CreateSavedReplyFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = SavedReplyFolder.Create(request.Name, request.SortOrder);
        dbContext.SavedReplyFolders.Add(folder);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(KnowledgeBaseMapper.ToDto(folder));
    }
}
