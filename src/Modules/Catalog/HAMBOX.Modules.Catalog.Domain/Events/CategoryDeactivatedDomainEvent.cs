using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Catalog.Domain.Events;

/// <summary>
/// Raised when a category is deactivated.
/// </summary>
/// <param name="CategoryId">The identifier of the deactivated category.</param>
public sealed record CategoryDeactivatedDomainEvent(Guid CategoryId) : BaseDomainEvent;
