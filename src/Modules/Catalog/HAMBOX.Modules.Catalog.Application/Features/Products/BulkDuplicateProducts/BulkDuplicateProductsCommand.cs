using System;
using System.Collections.Generic;
using HAMBOX.Modules.Catalog.Application.Features.Products.BulkProducts;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.BulkDuplicateProducts;

public sealed record BulkDuplicateProductsRequest(
    IReadOnlyList<Guid>? ProductIds,
    string? SearchTerm,
    ProductStatus? Status,
    Guid? CategoryId,
    bool SelectAllMatching,
    string? NameSuffix);

public sealed record BulkDuplicateProductsCommand(BulkDuplicateProductsRequest Request)
    : IRequest<Result<BulkProductsResultDto>>;
