using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Catalog.Domain.Events;

/// <summary>
/// Raised when a product is deactivated.
/// </summary>
/// <param name="ProductId">The identifier of the deactivated product.</param>
public sealed record ProductDeactivatedDomainEvent(Guid ProductId) : BaseDomainEvent;
