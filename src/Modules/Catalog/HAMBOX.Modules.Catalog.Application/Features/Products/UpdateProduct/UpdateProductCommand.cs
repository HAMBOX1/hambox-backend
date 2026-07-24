using System;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.UpdateProduct;

public record UpdateProductCommand(
    Guid Id,
    string NameAr,
    string NameEn,
    string DescriptionAr,
    string DescriptionEn,
    decimal Price,
    Guid CategoryId,
    ProductStatus Status,
    IReadOnlyList<Guid>? AdditionalCategoryIds = null,
    IReadOnlyList<Guid>? CollectionIds = null) : IRequest<Result>;
