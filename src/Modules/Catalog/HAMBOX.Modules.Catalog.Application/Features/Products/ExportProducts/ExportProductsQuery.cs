using System;
using System.Collections.Generic;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.ExportProducts;

public sealed record ExportProductsRequest(
    IReadOnlyList<Guid>? ProductIds,
    string? SearchTerm,
    ProductStatus? Status,
    Guid? CategoryId,
    bool SelectAllMatching);

public sealed record ExportProductsDto(string FileName, string ContentType, byte[] Content);

public sealed record ExportProductsQuery(ExportProductsRequest Request) : IRequest<Result<ExportProductsDto>>;
