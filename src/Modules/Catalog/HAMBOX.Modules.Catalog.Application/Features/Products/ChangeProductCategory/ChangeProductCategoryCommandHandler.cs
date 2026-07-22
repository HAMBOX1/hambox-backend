using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.ChangeProductCategory;

internal sealed class ChangeProductCategoryCommandHandler : IRequestHandler<ChangeProductCategoryCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public ChangeProductCategoryCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(ChangeProductCategoryCommand request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

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
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
