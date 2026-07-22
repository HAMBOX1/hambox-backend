using System;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.ChangeProductCategory;

public sealed record ChangeProductCategoryCommand(Guid ProductId, Guid CategoryId) : IRequest<Result>;
