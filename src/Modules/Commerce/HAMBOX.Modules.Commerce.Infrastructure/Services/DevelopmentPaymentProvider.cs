using HAMBOX.Modules.Commerce.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Development-only payment provider that simulates authorization and capture without an external PSP.
/// </summary>
internal sealed class DevelopmentPaymentProvider(
    IHostEnvironment environment,
    ILogger<DevelopmentPaymentProvider> logger) : IPaymentProvider
{
    public const string PaymentMethodKey = "development";
    public string ProviderName => "Development";

    public bool CanHandle(string paymentMethod) =>
        environment.IsDevelopment() &&
        string.Equals(paymentMethod, PaymentMethodKey, StringComparison.OrdinalIgnoreCase);

    public async Task<PaymentProviderResult> ProcessAsync(
        PaymentProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            return new PaymentProviderResult(
                false,
                "Failed",
                null,
                ProviderName,
                "Development checkout is only available in Development environment.");
        }

        if (!CanHandle(request.PaymentMethod))
        {
            return new PaymentProviderResult(
                false,
                "Failed",
                null,
                ProviderName,
                "Unsupported payment method for development provider.");
        }

        logger.LogInformation(
            "Development payment: authorizing {Amount} {Currency} for {Email}",
            request.Amount,
            request.Currency,
            request.CustomerEmail);

        await Task.Delay(450, cancellationToken);

        var authorizationId = $"DEV-AUTH-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        logger.LogInformation(
            "Development payment authorized: AuthorizationId={AuthorizationId}",
            authorizationId);

        await Task.Delay(550, cancellationToken);

        var transactionId = $"DEV-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        logger.LogInformation(
            "Development payment captured: TransactionId={TransactionId}, AuthorizationId={AuthorizationId}",
            transactionId,
            authorizationId);

        return new PaymentProviderResult(
            true,
            "Paid",
            transactionId,
            ProviderName);
    }
}
