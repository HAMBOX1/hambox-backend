using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Catalog.Domain.Events;

/// <summary>
/// Raised when a product is archived.
/// </summary>
/// <param name="ProductId">The identifier of the archived product.</param>
public sealed record ProductArchivedDomainEvent(Guid ProductId) : BaseDomainEvent;
