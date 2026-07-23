using System;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.CreateProduct;

public record CreateProductCommand(
    string NameAr,
    string NameEn,
    string DescriptionAr,
    string DescriptionEn,
    decimal Price,
    Guid CategoryId,
    IReadOnlyList<Guid>? AdditionalCategoryIds = null) : IRequest<Result<Guid>>;
