using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Legal.Domain.Legal;

/// <summary>
/// Records that a user accepted a specific published version of a legal section. One row per
/// (user, section) acceptance event — normalized so it scales to any number of sections, unlike
/// the fixed Terms/Privacy/Refund columns this replaces.
/// </summary>
public sealed class LegalSectionAcceptance : BaseEntity
{
    private LegalSectionAcceptance()
    {
    }

    private LegalSectionAcceptance(
        Guid id,
        string userId,
        Guid legalSectionId,
        int versionNumber,
        DateTimeOffset acceptedAtUtc,
        string ipAddress,
        string userAgent,
        string language,
        Guid? orderId)
        : base(id)
    {
        UserId = userId;
        LegalSectionId = legalSectionId;
        VersionNumber = versionNumber;
        AcceptedAtUtc = acceptedAtUtc;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Language = language;
        OrderId = orderId;
    }

    public string UserId { get; private set; } = string.Empty;
    public Guid LegalSectionId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateTimeOffset AcceptedAtUtc { get; private set; }
    public string IpAddress { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public string Language { get; private set; } = "en";

    /// <summary>
    /// The order this acceptance gated, when recorded at checkout rather than at registration.
    /// Null for the registration-time acceptance sweep, which predates any order existing.
    /// </summary>
    public Guid? OrderId { get; private set; }

    public static LegalSectionAcceptance Create(
        string userId,
        Guid legalSectionId,
        int versionNumber,
        string ipAddress,
        string userAgent,
        string language,
        Guid? orderId = null) =>
        new(Guid.NewGuid(), userId, legalSectionId, versionNumber, DateTimeOffset.UtcNow, ipAddress, userAgent, language, orderId);
}
