using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.SavedReplies;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.CreateSavedReply;

internal sealed class CreateSavedReplyCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<CreateSavedReplyCommand, Result<SavedReplyDto>>
{
    public async Task<Result<SavedReplyDto>> Handle(CreateSavedReplyCommand request, CancellationToken cancellationToken)
    {
        var reply = SavedReply.Create(request.FolderId, request.Title, request.Body);
        dbContext.SavedReplies.Add(reply);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(KnowledgeBaseMapper.ToDto(reply));
    }
}
