using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.GetProductInstructions;

public sealed record GetProductInstructionsQuery(Guid ProductId, Guid? VariantId = null) : IRequest<Result<ProductInstructionsDto>>;
