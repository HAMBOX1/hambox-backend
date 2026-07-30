using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.GetSavedReplyFolders;

internal sealed class GetSavedReplyFoldersQueryHandler(ISupportDbContext dbContext)
    : IRequestHandler<GetSavedReplyFoldersQuery, Result<IReadOnlyList<SavedReplyFolderDto>>>
{
    public async Task<Result<IReadOnlyList<SavedReplyFolderDto>>> Handle(GetSavedReplyFoldersQuery request, CancellationToken cancellationToken)
    {
        var folders = await dbContext.SavedReplyFolders.AsNoTracking().OrderBy(f => f.SortOrder).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SavedReplyFolderDto>>(folders.Select(KnowledgeBaseMapper.ToDto).ToList());
    }
}
