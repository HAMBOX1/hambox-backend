using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.GetSavedReplies;

internal sealed class GetSavedRepliesQueryHandler(ISupportDbContext dbContext)
    : IRequestHandler<GetSavedRepliesQuery, Result<IReadOnlyList<SavedReplyDto>>>
{
    public async Task<Result<IReadOnlyList<SavedReplyDto>>> Handle(GetSavedRepliesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.SavedReplies.AsNoTracking().AsQueryable();

        if (request.FolderId is not null)
        {
            query = query.Where(r => r.FolderId == request.FolderId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(r => r.Title.Contains(term) || r.Body.Contains(term));
        }

        var replies = await query.OrderBy(r => r.Title).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SavedReplyDto>>(replies.Select(KnowledgeBaseMapper.ToDto).ToList());
    }
}
