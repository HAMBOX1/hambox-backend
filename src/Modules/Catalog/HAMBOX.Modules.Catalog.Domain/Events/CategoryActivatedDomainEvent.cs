using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Catalog.Domain.Events;

/// <summary>
/// Raised when a category is activated.
/// </summary>
/// <param name="CategoryId">The identifier of the activated category.</param>
public sealed record CategoryActivatedDomainEvent(Guid CategoryId) : BaseDomainEvent;
