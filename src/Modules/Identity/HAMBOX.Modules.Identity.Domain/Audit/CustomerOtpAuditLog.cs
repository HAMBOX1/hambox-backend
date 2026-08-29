using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Domain.Audit;

/// <summary>
/// An immutable, append-only record of one event in a customer-facing OTP/verification-token
/// lifecycle (email verification, password reset). One row per event — issuance, use, a failed or
/// expired attempt, or invalidation by a resend — so the full history survives even after the
/// underlying token row is deleted or superseded. Never stores the plaintext token/code value.
/// </summary>
public sealed class CustomerOtpAuditLog : Entity
{
    private CustomerOtpAuditLog()
    {
    }

    private CustomerOtpAuditLog(
        Guid id,
        Guid? userId,
        Guid? tokenId,
        CustomerOtpPurpose purpose,
        CustomerOtpEventStatus status,
        DateTimeOffset? issuedOnUtc,
        DateTimeOffset? expiresOnUtc,
        DateTimeOffset? usedOnUtc,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        CustomerOtpEmailDeliveryStatus emailDeliveryStatus,
        string? description)
        : base(id)
    {
        UserId = userId;
        TokenId = tokenId;
        Purpose = purpose;
        Status = status;
        IssuedOnUtc = issuedOnUtc;
        ExpiresOnUtc = expiresOnUtc;
        UsedOnUtc = usedOnUtc;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CorrelationId = correlationId;
        EmailDeliveryStatus = emailDeliveryStatus;
        Description = description;
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>The account this event concerns. Null only if a failed attempt could not be
    /// attributed to any known token (e.g. a garbage/guessed value).</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Correlates every event row belonging to the same underlying token across its
    /// lifecycle (issued → used/expired/invalidated). Not a foreign key to any single table, since
    /// the referenced token may since have been deleted (e.g. superseded by a resend).</summary>
    public Guid? TokenId { get; private set; }

    public CustomerOtpPurpose Purpose { get; private set; }

    public CustomerOtpEventStatus Status { get; private set; }

    /// <summary>When the underlying token was originally issued. Null when the event could not be
    /// attributed to any known token (e.g. a failed attempt with an unrecognized value).</summary>
    public DateTimeOffset? IssuedOnUtc { get; private set; }

    /// <summary>Null when the event could not be attributed to any known token.</summary>
    public DateTimeOffset? ExpiresOnUtc { get; private set; }

    public DateTimeOffset? UsedOnUtc { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string? CorrelationId { get; private set; }

    public CustomerOtpEmailDeliveryStatus EmailDeliveryStatus { get; private set; }

    /// <summary>Short human-readable context, never the token/code value itself.</summary>
    public string? Description { get; private set; }

    /// <summary>When this audit row was recorded (as opposed to <see cref="IssuedOnUtc"/>, which is
    /// when the underlying token was created).</summary>
    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static CustomerOtpAuditLog Record(
        CustomerOtpPurpose purpose,
        CustomerOtpEventStatus status,
        DateTimeOffset? issuedOnUtc,
        DateTimeOffset? expiresOnUtc,
        Guid? userId = null,
        Guid? tokenId = null,
        DateTimeOffset? usedOnUtc = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? correlationId = null,
        CustomerOtpEmailDeliveryStatus emailDeliveryStatus = CustomerOtpEmailDeliveryStatus.NotApplicable,
        string? description = null)
    {
        return new CustomerOtpAuditLog(
            Guid.NewGuid(),
            userId,
            tokenId,
            purpose,
            status,
            issuedOnUtc,
            expiresOnUtc,
            usedOnUtc,
            ipAddress,
            userAgent,
            correlationId,
            emailDeliveryStatus,
            description);
    }
}
