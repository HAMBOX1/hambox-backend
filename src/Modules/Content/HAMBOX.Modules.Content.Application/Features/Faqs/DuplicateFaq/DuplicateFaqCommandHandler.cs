using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.DuplicateFaq;

internal sealed class DuplicateFaqCommandHandler(IContentDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DuplicateFaqCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(DuplicateFaqCommand request, CancellationToken cancellationToken)
    {
        var source = await dbContext.Faqs.AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (source is null)
        {
            return Result.Failure<Guid>(ContentErrors.FaqNotFound);
        }

        var maxSortOrder = await dbContext.Faqs.MaxAsync(f => (int?)f.SortOrder, cancellationToken) ?? 0;

        var duplicate = source.Duplicate();
        duplicate.SetSortOrder(maxSortOrder + 1);

        dbContext.Faqs.Add(duplicate);
        FaqAuditWriter.Record(dbContext, duplicate.Id, FaqAuditAction.Duplicated, currentUser.UserId, $"{{\"sourceId\":\"{source.Id}\"}}");
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(duplicate.Id);
    }
}
