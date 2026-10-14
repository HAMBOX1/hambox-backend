namespace HAMBOX.Application.Security;

/// <summary>
/// Lets modules outside Identity (checkout) ask whether a phone number is on the Security Center's blocklist
/// without depending on the Identity module. Implemented by Identity's blocklist service.
/// </summary>
public interface IPhoneBlocklist
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="phoneNumber"/> matches an active blocked-phone entry,
    /// whatever its formatting (<c>0100…</c>, <c>+20100…</c>, <c>0020100…</c>).
    /// </summary>
    Task<bool> IsPhoneBlockedAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
