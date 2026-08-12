using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.DeleteFaq;

internal sealed class DeleteFaqCommandHandler(IContentDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DeleteFaqCommand, Result>
{
    public async Task<Result> Handle(DeleteFaqCommand request, CancellationToken cancellationToken)
    {
        var faq = await dbContext.Faqs.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (faq is null)
        {
            return Result.Failure(ContentErrors.FaqNotFound);
        }

        faq.Delete();
        FaqAuditWriter.Record(dbContext, faq.Id, FaqAuditAction.Deleted, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
