using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Analytics;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration;

public sealed record GetStorefrontProductConfigurationQuery(Guid ProductId)
    : IRequest<Result<StorefrontProductConfigurationDto>>;

internal sealed class GetStorefrontProductConfigurationQueryHandler
    : IRequestHandler<GetStorefrontProductConfigurationQuery, Result<StorefrontProductConfigurationDto>>
{
    private readonly ICatalogDbContext _db;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetStorefrontProductConfigurationQueryHandler> _logger;

    public GetStorefrontProductConfigurationQueryHandler(
        ICatalogDbContext db,
        IInventoryEngine engine,
        ICurrentUserService currentUser,
        ILogger<GetStorefrontProductConfigurationQueryHandler> logger)
    {
        _db = db;
        _inventoryEngine = engine;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<StorefrontProductConfigurationDto>> Handle(
        GetStorefrontProductConfigurationQuery request,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null || product.Status != ProductStatus.Active)
        {
            return Result.Failure<StorefrontProductConfigurationDto>(CatalogErrors.ProductNotFound);
        }

        var groups = await _db.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .Where(g => g.ProductId == request.ProductId)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(cancellationToken);

        var optionGroupDtos = groups.Select(g => new ProductOptionGroupDto(
            g.Id, g.ProductId, g.ParentOptionId, g.Key, g.DisplayName, g.SortOrder, g.IsRequired,
            g.Options.OrderBy(o => o.SortOrder)
                .Select(o => new ProductOptionDto(o.Id, o.OptionGroupId, o.Value, o.Label, o.SortOrder))
                .ToList())).ToList();

        var variants = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.SelectedOptions)
            .Where(v => v.ProductId == request.ProductId && v.Status == ProductVariantStatus.Active && v.IsVisible)
            .OrderBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);

        var stock = variants.Count > 0
            ? await _inventoryEngine.GetVariantStockBulkAsync(variants.Select(v => v.Id), cancellationToken)
            : new Dictionary<Guid, VariantStockSnapshot>();

        var variantDtos = variants.Select(v =>
        {
            stock.TryGetValue(v.Id, out var s);
            return new StorefrontVariantDto(
                v.Id,
                v.Sku,
                v.PriceOverride ?? product.Price,
                v.ComparePrice,
                s?.Available ?? 0,
                s?.IsLowStock ?? false,
                s?.IsOutOfStock ?? true,
                v.SelectedOptions.Select(o => o.OptionId).ToList());
        }).ToList();

        await TryLogProductViewAsync(request.ProductId, cancellationToken);

        return Result.Success(new StorefrontProductConfigurationDto(
            product.Id,
            product.Price,
            optionGroupDtos,
            variantDtos));
    }

    private async Task TryLogProductViewAsync(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            _db.ProductViewEvents.Add(ProductViewEvent.Create(productId, _currentUser.UserId));
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Best-effort product view logging failed for {ProductId}.", productId);
        }
    }
}
