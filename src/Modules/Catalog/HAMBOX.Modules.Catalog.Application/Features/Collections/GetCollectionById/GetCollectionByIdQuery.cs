using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.GetCollectionById;

public record GetCollectionByIdQuery(Guid Id) : IRequest<Result<CollectionDto>>;
