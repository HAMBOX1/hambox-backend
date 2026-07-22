using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Application.Features.Products.BulkProducts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.BulkDuplicateProducts;

internal sealed class BulkDuplicateProductsCommandHandler : IRequestHandler<BulkDuplicateProductsCommand, Result<BulkProductsResultDto>>
{
    private readonly ICatalogDbContext _db;
    private readonly ISender _sender;

    public BulkDuplicateProductsCommandHandler(ICatalogDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<Result<BulkProductsResultDto>> Handle(BulkDuplicateProductsCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
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
            var result = await _sender.Send(new DuplicateProductCommand(productId, request.NameSuffix), cancellationToken);
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
