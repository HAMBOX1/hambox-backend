using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Identity.Domain.Users.Events;

/// <summary>
/// Raised when a user account is blocked.
/// </summary>
/// <param name="UserId">The identifier of the blocked user.</param>
public sealed record UserBlockedDomainEvent(Guid UserId) : BaseDomainEvent;
