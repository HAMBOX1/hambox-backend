using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;
using HAMBOX.Modules.Catalog.Application.Features.Products.ChangeProductCategory;
using HAMBOX.SharedKernel.Results;
using MediatR;
// PublishProductCommand / DeactivateProductCommand / ArchiveProductCommand live directly in Features.Products (ProductLifecycleHandlers.cs).
using HAMBOX.Modules.Catalog.Application.Features.Products;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.BulkProducts;

internal sealed class BulkProductsCommandHandler : IRequestHandler<BulkProductsCommand, Result<BulkProductsResultDto>>
{
    private readonly ICatalogDbContext _db;
    private readonly ISender _sender;

    public BulkProductsCommandHandler(ICatalogDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<Result<BulkProductsResultDto>> Handle(BulkProductsCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var action = request.Action.Trim().ToLowerInvariant();

        if (action == "change-category" && request.TargetCategoryId is null)
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBulkActionNotSupported(action));
        }

        if (action == "adjust-price" && (request.PriceMode is null || request.PriceValue is null))
        {
            return Result.Failure<BulkProductsResultDto>(CatalogErrors.ProductBulkActionNotSupported(action));
        }

        var selection = new BulkProductSelection(
            request.ProductIds, request.SearchTerm, request.Status, request.CategoryId, request.SelectAllMatching);

        var resolved = await BulkProductSelectionResolver.ResolveAsync(_db, selection, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<BulkProductsResultDto>(resolved.Error);
        }

        var success = 0;
        var errors = new List<string>();

        foreach (var productId in resolved.Value)
        {
            var result = action switch
            {
                "publish" => await _sender.Send(new PublishProductCommand(productId), cancellationToken),
                "unpublish" => await _sender.Send(new DeactivateProductCommand(productId), cancellationToken),
                "archive" => await _sender.Send(new ArchiveProductCommand(productId), cancellationToken),
                "change-category" => await _sender.Send(
                    new ChangeProductCategoryCommand(productId, request.TargetCategoryId!.Value), cancellationToken),
                "adjust-price" => await _sender.Send(
                    new AdjustProductPriceCommand(productId, request.PriceMode!.Value, request.PriceValue!.Value), cancellationToken),
                _ => Result.Failure(CatalogErrors.ProductBulkActionNotSupported(action)),
            };

            if (result.IsSuccess)
            {
                success++;
            }
            else
            {
                errors.Add($"{productId}: {result.Error.Description}");
            }
        }

        return Result.Success(new BulkProductsResultDto(success, errors.Count, errors));
    }
}
