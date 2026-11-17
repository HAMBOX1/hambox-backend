using System;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts;

public record GetProductsQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm,
    ProductStatus? Status,
    Guid? CategoryId,
    ProductSortBy? SortBy,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? AttributeFilters = null,
    Guid? CollectionId = null,
    bool PendingMergeOnly = false,
    bool FavoritesOnly = false,
    decimal? MinPrice = null,
    decimal? MaxPrice = null) : IRequest<Result<PagedResult<ProductDto>>>;
