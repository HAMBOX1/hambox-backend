using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Catalog.Domain.Events;

/// <summary>
/// Raised when a product is activated.
/// </summary>
/// <param name="ProductId">The identifier of the activated product.</param>
public sealed record ProductActivatedDomainEvent(Guid ProductId) : BaseDomainEvent;
