using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.SaveProductInstructions;

public sealed record SaveProductInstructionsCommand(
    Guid ProductId,
    Guid? VariantId,
    string Title,
    string ContentHtml) : IRequest<Result<ProductInstructionsDto>>;
