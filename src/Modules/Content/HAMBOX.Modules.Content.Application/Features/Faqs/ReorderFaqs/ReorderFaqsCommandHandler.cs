using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.ReorderFaqs;

internal sealed class ReorderFaqsCommandHandler(IContentDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<ReorderFaqsCommand, Result>
{
    public async Task<Result> Handle(ReorderFaqsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Entries.Select(e => e.Id).ToList();
        var faqsById = await dbContext.Faqs
            .Where(f => ids.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        foreach (var entry in request.Entries)
        {
            if (!faqsById.TryGetValue(entry.Id, out var faq))
            {
                continue;
            }

            faq.SetSortOrder(entry.SortOrder);
            FaqAuditWriter.Record(dbContext, faq.Id, FaqAuditAction.Reordered, currentUser.UserId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
