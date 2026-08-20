namespace HAMBOX.Modules.Commerce.Application.Contracts;

/// <summary>Returned from initiating a DOT checkout — the customer's browser must be redirected to <see cref="OtpLandingPageUrl"/> next.</summary>
public sealed record DotCheckoutInitiationDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string OtpLandingPageUrl,
    DateTimeOffset ExpiresOnUtc);

/// <summary>
/// Customer-safe polling status for a DOT payment attempt. Deliberately excludes anything
/// provider-internal (raw DOT response fields, reason codes DOT itself defines, MSISDN).
/// </summary>
public sealed record DotPaymentStatusDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string Status,
    Guid? CompletedOrderId);
