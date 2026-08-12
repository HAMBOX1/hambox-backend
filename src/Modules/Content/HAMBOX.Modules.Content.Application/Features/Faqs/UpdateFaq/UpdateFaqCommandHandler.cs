using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaq;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.UpdateFaq;

internal sealed class UpdateFaqCommandHandler(
    IContentDbContext dbContext,
    ICatalogDbContext catalogDbContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateFaqCommand, Result>
{
    public async Task<Result> Handle(UpdateFaqCommand request, CancellationToken cancellationToken)
    {
        var faq = await dbContext.Faqs.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
        if (faq is null)
        {
            return Result.Failure(ContentErrors.FaqNotFound);
        }

        var categoryExists = await dbContext.FaqCategories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return Result.Failure(ContentErrors.FaqCategoryNotFound);
        }

        var targetError = await CreateFaqCommandHandler.ValidateTargetAsync(
            request.Scope, request.TargetId, catalogDbContext, cancellationToken);
        if (targetError is not null)
        {
            return Result.Failure(targetError);
        }

        // Sanitized separately from FluentValidation's NotEmpty check (which runs on the raw,
        // pre-sanitized input): an answer that is entirely unsafe markup sanitizes down to
        // nothing, which must fail cleanly here rather than reach Faq.Update's
        // ArgumentException.ThrowIfNullOrWhiteSpace as an unhandled exception.
        var sanitizedAnswerEn = FaqContentSanitizer.Sanitize(request.AnswerEn)!;
        if (string.IsNullOrWhiteSpace(sanitizedAnswerEn))
        {
            return Result.Failure(ContentErrors.FaqAnswerInvalid);
        }

        faq.Update(
            request.QuestionEn,
            request.QuestionAr,
            sanitizedAnswerEn,
            FaqContentSanitizer.Sanitize(request.AnswerAr),
            request.CategoryId,
            request.Scope,
            request.TargetId);

        FaqAuditWriter.Record(dbContext, faq.Id, FaqAuditAction.Updated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
