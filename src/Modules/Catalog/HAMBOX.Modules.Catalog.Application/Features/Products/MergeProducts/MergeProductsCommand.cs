using System;
using System.Collections.Generic;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.MergeProducts;

/// <summary>
/// Merges <paramref name="SourceProductIds"/> into <paramref name="TargetProductId"/>: each source
/// product becomes a <see cref="Domain.Inventory.ProductVariant"/> of the target (keeping its own
/// price) and is then soft-deleted. Used to collapse near-duplicate products (e.g. "Xbox 1€",
/// "Xbox 2€", ...) that were added as separate products but are really denominations of one item.
/// </summary>
public sealed record MergeProductsCommand(
    Guid TargetProductId,
    IReadOnlyList<Guid> SourceProductIds,
    bool ConfirmStockLoss) : IRequest<Result<MergeProductsResultDto>>;

public sealed record MergeProductsResultDto(
    Guid TargetProductId,
    IReadOnlyList<Guid> CreatedVariantIds,
    int MergedSourceCount);
