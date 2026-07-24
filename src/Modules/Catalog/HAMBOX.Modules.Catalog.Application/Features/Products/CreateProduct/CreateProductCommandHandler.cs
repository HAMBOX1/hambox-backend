using System;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.CreateProduct;

internal sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _dbContext;

    public CreateProductCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return Result.Failure<Guid>(CatalogErrors.CategoryNotFound);
        }

        var additionalCategoryIds = request.AdditionalCategoryIds ?? [];
        if (additionalCategoryIds.Count > 0)
        {
            var existingCount = await _dbContext.Categories
                .Where(c => additionalCategoryIds.Contains(c.Id))
                .Select(c => c.Id)
                .Distinct()
                .CountAsync(cancellationToken);

            if (existingCount != additionalCategoryIds.Distinct().Count())
            {
                return Result.Failure<Guid>(CatalogErrors.CategoryNotFound);
            }
        }

        var collectionIds = request.CollectionIds ?? [];
        if (collectionIds.Count > 0)
        {
            var existingCollectionCount = await _dbContext.ProductCollections
                .Where(c => collectionIds.Contains(c.Id))
                .Select(c => c.Id)
                .Distinct()
                .CountAsync(cancellationToken);

            if (existingCollectionCount != collectionIds.Distinct().Count())
            {
                return Result.Failure<Guid>(CatalogErrors.CollectionNotFound);
            }
        }

        var product = Product.Create(
            request.NameAr,
            request.NameEn,
            request.DescriptionAr,
            request.DescriptionEn,
            request.Price,
            request.CategoryId);

        var newAdditionalCategories = product.SetAdditionalCategories(additionalCategoryIds);
        var newCollectionItems = product.SetCollections(collectionIds);

        _dbContext.Products.Add(product);
        foreach (var newAdditionalCategory in newAdditionalCategories)
        {
            _dbContext.ProductCategories.Add(newAdditionalCategory);
        }
        foreach (var newCollectionItem in newCollectionItems)
        {
            _dbContext.ProductCollectionItems.Add(newCollectionItem);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(product.Id);
    }
}
