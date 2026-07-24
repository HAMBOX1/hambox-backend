using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.RemoveProductCollection;

public record RemoveProductCollectionCommand(Guid ProductId, Guid CollectionId) : IRequest<Result>;
