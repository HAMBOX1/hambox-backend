using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.DeleteCollection;

public record DeleteCollectionCommand(Guid Id) : IRequest<Result>;
