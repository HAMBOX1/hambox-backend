namespace HAMBOX.Modules.Commerce.Application.Abstractions;

public interface IPaymentProvider
{
    string ProviderName { get; }

    bool CanHandle(string paymentMethod);

    Task<PaymentProviderResult> ProcessAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default);
}

public sealed record PaymentProviderRequest(
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string CustomerEmail,
    string? UserId);

public sealed record PaymentProviderResult(
    bool IsSuccess,
    string PaymentStatus,
    string? TransactionId,
    string Provider,
    string? FailureReason = null);
