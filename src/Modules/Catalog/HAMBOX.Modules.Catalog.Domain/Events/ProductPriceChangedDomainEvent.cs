using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Catalog.Domain.Events;

/// <summary>
/// Raised when the price of a product is changed.
/// </summary>
/// <param name="ProductId">The identifier of the product.</param>
/// <param name="OldPrice">The previous price.</param>
/// <param name="NewPrice">The new price.</param>
public sealed record ProductPriceChangedDomainEvent(
    Guid ProductId,
    decimal OldPrice,
    decimal NewPrice) : BaseDomainEvent;
