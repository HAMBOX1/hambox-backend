using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.UnpublishProductInstructions;

internal sealed class UnpublishProductInstructionsCommandHandler(ICatalogDbContext dbContext)
    : IRequestHandler<UnpublishProductInstructionsCommand, Result<ProductInstructionsDto>>
{
    public async Task<Result<ProductInstructionsDto>> Handle(
        UnpublishProductInstructionsCommand request,
        CancellationToken cancellationToken)
    {
        var instructions = await dbContext.ProductInstructions
            .FirstOrDefaultAsync(i => i.ProductId == request.ProductId, cancellationToken);

        if (instructions is null)
        {
            return Result.Failure<ProductInstructionsDto>(CatalogErrors.InstructionsNotFound);
        }

        instructions.Unpublish();
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
