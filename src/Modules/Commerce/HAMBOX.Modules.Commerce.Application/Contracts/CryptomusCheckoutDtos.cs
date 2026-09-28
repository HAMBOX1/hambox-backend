namespace HAMBOX.Modules.Commerce.Application.Contracts;

/// <summary>Returned from initiating a Cryptomus checkout — the customer's browser must be redirected to <see cref="PaymentUrl"/> next.</summary>
public sealed record CryptomusCheckoutInitiationDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string PaymentUrl,
    DateTimeOffset ExpiresOnUtc);

/// <summary>
/// Customer-safe polling status for a Cryptomus payment attempt. Deliberately excludes anything
/// provider-internal (raw Cryptomus response fields).
/// </summary>
public sealed record CryptomusPaymentStatusDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string Status,
    Guid? CompletedOrderId);
