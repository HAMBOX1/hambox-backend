using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.SetFaqPublishState;

internal sealed class SetFaqPublishStateCommandHandler(IContentDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<SetFaqPublishStateCommand, Result>
{
    public async Task<Result> Handle(SetFaqPublishStateCommand request, CancellationToken cancellationToken)
    {
        var faq = await dbContext.Faqs.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (faq is null)
        {
            return Result.Failure(ContentErrors.FaqNotFound);
        }

        if (request.Publish)
        {
            faq.Publish();
        }
        else
        {
            faq.Unpublish();
        }

        FaqAuditWriter.Record(
            dbContext, faq.Id, request.Publish ? FaqAuditAction.Published : FaqAuditAction.Unpublished, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
