using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.GetCollectionTree;

/// <summary>
/// Fetches every collection (unpaged) with sibling order and child/product counts,
/// so the admin tree explorer can render the full hierarchy in one call.
/// </summary>
public sealed record GetCollectionTreeQuery : IRequest<Result<IReadOnlyList<CollectionTreeItemDto>>>;
