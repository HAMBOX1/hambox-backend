namespace HAMBOX.Modules.Commerce.Application.Contracts;

/// <summary>Returned from initiating an OxaPay checkout — the customer's browser must be redirected to <see cref="PaymentUrl"/> next.</summary>
public sealed record OxaPayCheckoutInitiationDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string PaymentUrl,
    DateTimeOffset ExpiresOnUtc);

/// <summary>
/// Customer-safe polling status for an OxaPay payment attempt. Deliberately excludes anything
/// provider-internal (raw OxaPay response fields).
/// </summary>
public sealed record OxaPayPaymentStatusDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string Status,
    Guid? CompletedOrderId);
