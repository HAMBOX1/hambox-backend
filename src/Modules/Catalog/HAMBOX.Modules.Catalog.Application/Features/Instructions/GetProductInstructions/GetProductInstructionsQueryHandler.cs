using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.GetProductInstructions;

internal sealed class GetProductInstructionsQueryHandler(ICatalogDbContext dbContext)
    : IRequestHandler<GetProductInstructionsQuery, Result<ProductInstructionsDto>>
{
    public async Task<Result<ProductInstructionsDto>> Handle(
        GetProductInstructionsQuery request,
        CancellationToken cancellationToken)
    {
        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.ProductId, cancellationToken);

        if (!productExists)
        {
            return Result.Failure<ProductInstructionsDto>(CatalogErrors.ProductNotFound);
        }

        var instructions = await dbContext.ProductInstructions
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProductId == request.ProductId, cancellationToken);

        // A product with no authored instructions yet is a normal state, not an error —
        // the admin editor starts from a blank document.
        if (instructions is null)
        {
            return Result.Success(new ProductInstructionsDto(request.ProductId, string.Empty, string.Empty, 0, false, null));
        }

        return Result.Success(new ProductInstructionsDto(
            instructions.ProductId,
            instructions.Title,
            instructions.ContentHtml,
            instructions.Version,
            instructions.IsPublished,
            instructions.ModifiedOnUtc ?? instructions.CreatedOnUtc));
    }
}
