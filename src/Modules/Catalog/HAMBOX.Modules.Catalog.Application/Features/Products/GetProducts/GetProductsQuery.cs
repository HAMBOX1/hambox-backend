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
    ProductSortBy? SortBy) : IRequest<Result<PagedResult<ProductDto>>>;
