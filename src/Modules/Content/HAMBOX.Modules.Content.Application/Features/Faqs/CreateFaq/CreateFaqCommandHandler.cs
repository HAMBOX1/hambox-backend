using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaq;

internal sealed class CreateFaqCommandHandler(
    IContentDbContext dbContext,
    ICatalogDbContext catalogDbContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateFaqCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateFaqCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await dbContext.FaqCategories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return Result.Failure<Guid>(ContentErrors.FaqCategoryNotFound);
        }

        var targetError = await ValidateTargetAsync(request.Scope, request.TargetId, catalogDbContext, cancellationToken);
        if (targetError is not null)
        {
            return Result.Failure<Guid>(targetError);
        }

        // Sanitized separately from FluentValidation's NotEmpty check (which runs on the raw,
        // pre-sanitized input): an answer that is entirely unsafe markup (e.g. just "<script>...")
        // sanitizes down to nothing, which must fail cleanly here rather than reach Faq.Create's
        // ArgumentException.ThrowIfNullOrWhiteSpace as an unhandled exception.
        var sanitizedAnswerEn = FaqContentSanitizer.Sanitize(request.AnswerEn)!;
        if (string.IsNullOrWhiteSpace(sanitizedAnswerEn))
        {
            return Result.Failure<Guid>(ContentErrors.FaqAnswerInvalid);
        }

        var faq = Faq.Create(
            request.QuestionEn,
            request.QuestionAr,
            sanitizedAnswerEn,
            FaqContentSanitizer.Sanitize(request.AnswerAr),
            request.CategoryId,
            request.Scope,
            request.TargetId,
            request.SortOrder);

        dbContext.Faqs.Add(faq);
        FaqAuditWriter.Record(dbContext, faq.Id, FaqAuditAction.Created, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(faq.Id);
    }

    internal static async Task<Error?> ValidateTargetAsync(
        FaqScope scope, Guid? targetId, ICatalogDbContext catalogDbContext, CancellationToken cancellationToken)
    {
        switch (scope)
        {
            case FaqScope.Product:
                var productExists = await catalogDbContext.Products
                    .AnyAsync(p => p.Id == targetId!.Value, cancellationToken);
                return productExists ? null : ContentErrors.FaqTargetProductNotFound;

            case FaqScope.Category:
                var categoryExists = await catalogDbContext.Categories
                    .AnyAsync(c => c.Id == targetId!.Value, cancellationToken);
                return categoryExists ? null : ContentErrors.FaqTargetCategoryNotFound;

            default:
                return null;
        }
    }
}
