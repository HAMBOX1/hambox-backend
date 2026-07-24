using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.PublishProductInstructions;

public sealed record PublishProductInstructionsCommand(Guid ProductId) : IRequest<Result<ProductInstructionsDto>>;
