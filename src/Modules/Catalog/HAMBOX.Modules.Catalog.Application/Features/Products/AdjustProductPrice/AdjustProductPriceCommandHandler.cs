using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;

internal sealed class AdjustProductPriceCommandHandler : IRequestHandler<AdjustProductPriceCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public AdjustProductPriceCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(AdjustProductPriceCommand request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        var newPrice = request.Mode switch
        {
            PriceAdjustmentMode.IncreasePercent => product.Price * (1 + request.Value / 100m),
            PriceAdjustmentMode.DecreasePercent => product.Price * (1 - request.Value / 100m),
            PriceAdjustmentMode.SetFixed => request.Value,
            _ => product.Price,
        };

        if (newPrice < 0)
        {
            return Result.Failure(CatalogErrors.InvalidPriceAdjustment);
        }

        product.ChangePrice(decimal.Round(newPrice, 2));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
