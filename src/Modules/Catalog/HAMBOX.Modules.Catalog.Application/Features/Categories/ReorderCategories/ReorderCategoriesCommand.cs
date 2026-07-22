using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.ReorderCategories;

/// <summary>
/// One category's new position: its parent (unchanged or reparented) and its
/// sibling order under that parent.
/// </summary>
public sealed record CategoryReorderEntry(Guid Id, Guid? ParentId, int SortOrder);

/// <summary>
/// Applies a batch of sibling-order/parent changes produced by one drag-and-drop
/// move in the admin category tree (reordering siblings and/or reparenting).
/// </summary>
public sealed record ReorderCategoriesCommand(IReadOnlyList<CategoryReorderEntry> Entries) : IRequest<Result>;
