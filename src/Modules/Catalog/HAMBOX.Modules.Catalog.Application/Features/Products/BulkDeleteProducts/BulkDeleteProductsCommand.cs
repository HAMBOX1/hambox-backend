using System;
using System.Collections.Generic;
using HAMBOX.Modules.Catalog.Application.Features.Products.BulkProducts;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.BulkDeleteProducts;

public sealed record BulkDeleteProductsRequest(
    IReadOnlyList<Guid>? ProductIds,
    string? SearchTerm,
    ProductStatus? Status,
    Guid? CategoryId,
    bool SelectAllMatching);

public sealed record BulkDeleteProductsCommand(BulkDeleteProductsRequest Request)
    : IRequest<Result<BulkProductsResultDto>>;
