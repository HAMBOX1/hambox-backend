using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// Sets one price tier (<c>cost</c> or <c>member</c>) on every variant of a product in one go, so the catalog
/// list can edit the tiers at product level instead of variant by variant. A null value clears the tier.
/// </summary>
public sealed record SetProductVariantPricesCommand(Guid ProductId, string Field, decimal? Value)
    : IRequest<Result<int>>;

internal sealed class SetProductVariantPricesCommandHandler(ICatalogDbContext db)
    : IRequestHandler<SetProductVariantPricesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SetProductVariantPricesCommand request, CancellationToken cancellationToken)
    {
        var field = request.Field?.Trim().ToLowerInvariant();
        if (field is not ("cost" or "member") || request.Value is < 0)
        {
            return Result.Failure<int>(CatalogErrors.VariantNotFound);
        }

        var variants = await db.ProductVariants
            .Where(v => v.ProductId == request.ProductId && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
            return Result.Failure<int>(CatalogErrors.VariantNotFound);
        }

        foreach (var variant in variants)
        {
            if (field == "cost")
            {
                variant.SetCostPrice(request.Value);
            }
            else
            {
                variant.SetMemberPrice(request.Value);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(variants.Count);
    }
}
