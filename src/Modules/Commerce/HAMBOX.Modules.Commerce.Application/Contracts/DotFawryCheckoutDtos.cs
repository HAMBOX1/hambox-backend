namespace HAMBOX.Modules.Commerce.Application.Contracts;

/// <summary>
/// Returned from initiating a DOT Fawry checkout. <see cref="FawryReferenceNumber"/> is populated
/// only while the attempt is genuinely awaiting the customer's Fawry payment (resultCode "1000") —
/// null if the Direct Billing call already resolved (immediate success or failure), in which case
/// the frontend's status poll picks up the terminal state on its first tick.
/// </summary>
public sealed record DotFawryCheckoutInitiationDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string? FawryReferenceNumber,
    DateTimeOffset ExpiresOnUtc);

/// <summary>
/// Customer-safe polling status for a DOT Fawry payment attempt. Deliberately excludes anything
/// provider-internal (raw DOT response fields, MSISDN).
/// </summary>
public sealed record DotFawryPaymentStatusDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string Status,
    string? FawryReferenceNumber,
    Guid? CompletedOrderId);
