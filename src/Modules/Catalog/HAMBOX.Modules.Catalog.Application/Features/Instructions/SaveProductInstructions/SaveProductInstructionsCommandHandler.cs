using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Instructions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.SaveProductInstructions;

internal sealed class SaveProductInstructionsCommandHandler(ICatalogDbContext dbContext)
    : IRequestHandler<SaveProductInstructionsCommand, Result<ProductInstructionsDto>>
{
    public async Task<Result<ProductInstructionsDto>> Handle(
        SaveProductInstructionsCommand request,
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
            .FirstOrDefaultAsync(i => i.ProductId == request.ProductId, cancellationToken);

        if (instructions is null)
        {
            instructions = ProductInstructions.CreateDraft(request.ProductId, request.Title, request.ContentHtml);
            dbContext.ProductInstructions.Add(instructions);
        }
        else
        {
            instructions.SaveDraft(request.Title, request.ContentHtml);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ProductInstructionsDto(
            instructions.ProductId,
            instructions.Title,
            instructions.ContentHtml,
            instructions.Version,
            instructions.IsPublished,
            instructions.ModifiedOnUtc ?? instructions.CreatedOnUtc));
    }
}
