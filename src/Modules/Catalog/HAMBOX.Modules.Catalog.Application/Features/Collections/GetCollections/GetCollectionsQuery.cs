using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.GetCollections;

public record GetCollectionsQuery(int PageNumber, int PageSize, string? SearchTerm) : IRequest<Result<PagedResult<CollectionDto>>>;
