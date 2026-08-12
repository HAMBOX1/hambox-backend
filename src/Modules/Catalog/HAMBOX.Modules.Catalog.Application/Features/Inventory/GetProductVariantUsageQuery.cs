using HAMBOX.Application.Variants;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// Read-only categorized usage inspection for one variant. Never mutates anything — safe to call
/// as often as the admin UI wants (e.g. after every cleanup step) without side effects.
/// </summary>
public sealed record GetProductVariantUsageQuery(Guid VariantId) : IRequest<Result<VariantUsageDto>>;

internal sealed class GetProductVariantUsageQueryHandler : IRequestHandler<GetProductVariantUsageQuery, Result<VariantUsageDto>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICommerceVariantUsageProvider _commerceUsage;

    public GetProductVariantUsageQueryHandler(ICatalogDbContext db, ICommerceVariantUsageProvider commerceUsage)
    {
        _db = db;
        _commerceUsage = commerceUsage;
    }

    public async Task<Result<VariantUsageDto>> Handle(GetProductVariantUsageQuery request, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters(): usage inspection must also work for a variant an admin already
        // archived (Status = Archived, IsDeleted still false — unaffected by the global filter) as
        // well as, for completeness, one that was already permanently deleted (IsDeleted = true).
        var exists = await _db.ProductVariants
            .IgnoreQueryFilters()
            .AnyAsync(v => v.Id == request.VariantId, cancellationToken);

        if (!exists)
        {
            return Result.Failure<VariantUsageDto>(CatalogErrors.VariantNotFound);
        }

        var usage = await VariantUsageCalculator.ComputeAsync(_db, _commerceUsage, request.VariantId, cancellationToken);
        return Result.Success(usage);
    }
}
