using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Identity.Domain.Users.Events;

/// <summary>
/// Raised when a user account is activated.
/// </summary>
/// <param name="UserId">The identifier of the activated user.</param>
public sealed record UserActivatedDomainEvent(Guid UserId) : BaseDomainEvent;
