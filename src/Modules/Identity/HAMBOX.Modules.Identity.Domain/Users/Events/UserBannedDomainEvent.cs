using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Identity.Domain.Users.Events;

/// <summary>
/// Raised when a user account is permanently banned.
/// </summary>
/// <param name="UserId">The identifier of the banned user.</param>
public sealed record UserBannedDomainEvent(Guid UserId) : BaseDomainEvent;
