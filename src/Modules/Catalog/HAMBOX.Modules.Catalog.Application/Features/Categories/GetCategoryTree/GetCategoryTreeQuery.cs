using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryTree;

/// <summary>
/// Fetches every category (unpaged) with sibling order and child/product counts,
/// so the admin tree explorer can render the full hierarchy in one call.
/// </summary>
public sealed record GetCategoryTreeQuery : IRequest<Result<IReadOnlyList<CategoryTreeItemDto>>>;
