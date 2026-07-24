using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.AssignProductCollection;

public record AssignProductCollectionCommand(Guid ProductId, Guid CollectionId) : IRequest<Result>;
