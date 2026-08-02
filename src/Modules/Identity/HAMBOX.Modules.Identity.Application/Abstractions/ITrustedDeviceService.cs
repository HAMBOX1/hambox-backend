using HAMBOX.Modules.Identity.Domain.Sessions;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Tracks devices recognized across a user's login attempts (see <see cref="TrustedDevice"/>).
/// Used by the login flow to check for a blocked device and to record/upsert the device seen on
/// a successful login — kept out of the login handlers themselves since both the customer and
/// admin login flows need the identical logic.
/// </summary>
public interface ITrustedDeviceService
{
    /// <summary>
    /// Returns whether the given user has explicitly blocked this device fingerprint.
    /// </summary>
    Task<bool> IsDeviceBlockedAsync(Guid userId, string fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful login from this device — creating the <see cref="TrustedDevice"/> row
    /// if this is the first time it's been seen for this user, otherwise refreshing its
    /// "last seen" signals. Stages the change on the shared <see cref="IIdentityDbContext"/>
    /// without calling <c>SaveChangesAsync</c> — the caller persists it alongside the
    /// <see cref="LoginHistory"/> row it's writing in the same request.
    /// </summary>
    /// <returns><see langword="true"/> if this device had never been seen before for this user.</returns>
    Task<bool> RecordLoginAsync(
        Guid userId,
        string fingerprint,
        LoginContext context,
        string ipAddress,
        CancellationToken cancellationToken = default);
}
