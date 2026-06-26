using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.DeleteProduct;

internal sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;
    private readonly IFileStorage _fileStorage;

    public DeleteProductCommandHandler(ICatalogDbContext dbContext, IFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
    }

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .Include(entry => entry.Images)
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken);

        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        var storageKeys = product.Images
            .Select(image => image.StorageKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToList();

        _dbContext.Products.Remove(product);

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var storageKey in storageKeys)
        {
            await _fileStorage.DeleteAsync(storageKey, cancellationToken);
        }

        return Result.Success();
    }
}
