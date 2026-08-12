using HAMBOX.Application.Variants;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// Removes only category-A "safe to remove" references for a variant (active reservations,
/// Available/Disabled codes, cart items) — never touches sold codes, order history, license
/// keys, inventory batches, or audit logs. Returns the refreshed usage so the caller never has
/// to assume the cleanup succeeded.
/// </summary>
public sealed record CleanupProductVariantCommand(Guid VariantId) : IRequest<Result<VariantUsageDto>>;

internal sealed class CleanupProductVariantCommandHandler : IRequestHandler<CleanupProductVariantCommand, Result<VariantUsageDto>>
{
    private readonly ICatalogDbContext _db;
    private readonly IInventoryEngine _engine;
    private readonly ICommerceVariantUsageProvider _commerceUsage;

    public CleanupProductVariantCommandHandler(ICatalogDbContext db, IInventoryEngine engine, ICommerceVariantUsageProvider commerceUsage)
    {
        _db = db;
        _engine = engine;
        _commerceUsage = commerceUsage;
    }

    public async Task<Result<VariantUsageDto>> Handle(CleanupProductVariantCommand request, CancellationToken cancellationToken)
    {
        var exists = await _db.ProductVariants
            .IgnoreQueryFilters()
            .AnyAsync(v => v.Id == request.VariantId, cancellationToken);

        if (!exists)
        {
            return Result.Failure<VariantUsageDto>(CatalogErrors.VariantNotFound);
        }

        // Commerce-side cart items are removed first: they're the least consequential category
        // and Catalog cannot share one DB transaction with Commerce (separate schemas — only
        // Commerce's ICommerceTransactionService can orchestrate both, and reaching for that here
        // would reverse the Catalog->Commerce module boundary). If the Catalog-side step below
        // fails, retrying is still safe: cart items are already gone (idempotent no-op) and the
        // Catalog engine call runs as its own single atomic transaction.
        await _commerceUsage.RemoveCartItemsAsync(request.VariantId, cancellationToken);

        await _engine.CleanupVariantAsync(request.VariantId, cancellationToken);

        var usage = await VariantUsageCalculator.ComputeAsync(_db, _commerceUsage, request.VariantId, cancellationToken);
        return Result.Success(usage);
    }
}
