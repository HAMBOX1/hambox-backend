using System;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.DeleteProduct;

public record DeleteProductCommand(Guid Id) : IRequest<Result>;
