namespace HAMBOX.Modules.Catalog.Application.Abstractions;

/// <summary>One variant being re-parented from <see cref="FromProductId"/> to <see cref="ToProductId"/> by a merge.</summary>
public sealed record VariantMove(Guid VariantId, Guid FromProductId, Guid ToProductId);

/// <summary>
/// Keeps the Suppliers module's references (supplier mappings, derived prices) pointing at the right product
/// when a merge moves a variant under another product. Fulfillment routing resolves a supplier by
/// <c>InternalProductId</c>, so skipping this would silently break supplier delivery for the moved variant.
/// Implemented in Catalog.Infrastructure (the only layer that references Suppliers); it writes through the
/// Suppliers context, so call it inside <see cref="ICatalogSuppliersTransactionService"/> to commit atomically
/// with the Catalog changes.
/// </summary>
public interface IVariantSupplierLinkMover
{
    Task MoveAsync(IReadOnlyCollection<VariantMove> moves, CancellationToken cancellationToken);
}
