using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.UpdateProduct;

internal sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public UpdateProductCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        if (request.CategoryId != product.CategoryId)
        {
            var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
            if (!categoryExists)
            {
                return Result.Failure(CatalogErrors.CategoryNotFound);
            }
            product.ChangeCategory(request.CategoryId);
        }

        product.Update(request.NameAr, request.NameEn, request.DescriptionAr, request.DescriptionEn);
        product.ChangePrice(request.Price);

        if (request.Status != product.Status)
        {
            try
            {
                switch (request.Status)
                {
                    case ProductStatus.Active:
                        product.Activate();
                        break;
                    case ProductStatus.Inactive:
                        product.Deactivate();
                        break;
                    case ProductStatus.Archived:
                        product.Archive();
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                return Result.Failure(CatalogErrors.InvalidProductStatusTransition);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
