using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.AddPromotionTargets;

/// <summary>
/// The product page's "Add promo" / bulk "Apply promo code": attaches products (or whole categories) to an existing
/// promotion without having to open it and pick them from a long list. Already-targeted ones are skipped, so it is
/// safe to repeat. Note that a promotion with targets discounts only those products/categories.
/// </summary>
public sealed record AddPromotionTargetsCommand(Guid PromotionId, AddPromotionTargetsRequest Request)
    : IRequest<Result<AddPromotionTargetsResultDto>>;

internal sealed class AddPromotionTargetsCommandHandler(
    ICommerceDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<AddPromotionTargetsCommand, Result<AddPromotionTargetsResultDto>>
{
    public const int MaxTargetsPerRequest = 5000;

    public async Task<Result<AddPromotionTargetsResultDto>> Handle(
        AddPromotionTargetsCommand command, CancellationToken cancellationToken)
    {
        var productIds = (command.Request.ProductIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        var categoryIds = (command.Request.CategoryIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();

        if (productIds.Count + categoryIds.Count == 0)
        {
            return Result.Failure<AddPromotionTargetsResultDto>(CommerceErrors.PromotionTargetsEmpty);
        }

        if (productIds.Count + categoryIds.Count > MaxTargetsPerRequest)
        {
            return Result.Failure<AddPromotionTargetsResultDto>(CommerceErrors.PromotionTargetsTooMany);
        }

        var promotion = await dbContext.Promotions
            .Include(p => p.Targets)
            .FirstOrDefaultAsync(p => p.Id == command.PromotionId, cancellationToken);
        if (promotion is null)
        {
            return Result.Failure<AddPromotionTargetsResultDto>(CommerceErrors.PromotionNotFound);
        }

        var requested = productIds.Select(id => (PromotionTargetType.Product, id))
            .Concat(categoryIds.Select(id => (PromotionTargetType.Category, id)))
            .ToList();

        // The promotion is tracked as Unchanged (loaded via Include); new targets carry pre-set Guid keys, so they must be
        // registered explicitly or EF would treat them as existing rows.
        var added = promotion.AddTargetsIfMissing(requested);
        dbContext.PromotionTargets.AddRange(added);

        var productsAdded = added.Count(t => t.TargetType == PromotionTargetType.Product);
        var categoriesAdded = added.Count(t => t.TargetType == PromotionTargetType.Category);

        if (added.Count > 0)
        {
            PromotionAuditWriter.Write(
                dbContext,
                promotion.Id,
                null,
                PromotionAuditAction.Updated,
                currentUserService.UserId,
                $"Promotion '{promotion.Name}': added {productsAdded} product(s) and {categoriesAdded} categor{(categoriesAdded == 1 ? "y" : "ies")} as targets.");
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new AddPromotionTargetsResultDto(productsAdded, categoriesAdded, requested.Count - added.Count));
    }
}
