using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.ReorderCollections;

/// <summary>
/// One collection's new position: its parent (unchanged or reparented) and its
/// sibling order under that parent.
/// </summary>
public sealed record CollectionReorderEntry(Guid Id, Guid? ParentId, int SortOrder);

/// <summary>
/// Applies a batch of sibling-order/parent changes produced by one drag-and-drop
/// move in the admin collection tree (reordering siblings and/or reparenting).
/// </summary>
public sealed record ReorderCollectionsCommand(IReadOnlyList<CollectionReorderEntry> Entries) : IRequest<Result>;
