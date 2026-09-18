using System;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.GetProductStatusCounts;

/// <summary>
/// Counts products per <see cref="Domain.Enums.ProductStatus"/>, scoped by the same search/category/
/// collection filters as <see cref="GetProducts.GetProductsQuery"/> but independent of any status
/// filter — used to badge the catalog page's status tabs regardless of which tab is active.
/// </summary>
public sealed record GetProductStatusCountsQuery(
    string? SearchTerm,
    Guid? CategoryId,
    Guid? CollectionId) : IRequest<Result<ProductStatusCountsDto>>;

public sealed record ProductStatusCountsDto(int All, int Draft, int Active, int Inactive, int Archived, int PendingMerge = 0);
