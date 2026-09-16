using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.UnpublishProductInstructions;

public sealed record UnpublishProductInstructionsCommand(Guid ProductId, Guid? VariantId = null) : IRequest<Result<ProductInstructionsDto>>;
