using HAMBOX.Modules.Commerce.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Temporary provider that completes checkout without an external PSP.
/// Used for non-development payment methods until Stripe/PayPal is integrated.
/// </summary>
internal sealed class ImmediatePaymentProvider(
    IHostEnvironment environment,
    ILogger<ImmediatePaymentProvider> logger) : IPaymentProvider
{
    public string ProviderName => "Immediate";

    public bool CanHandle(string paymentMethod) =>
        environment.IsDevelopment() &&
        !string.Equals(paymentMethod, DevelopmentPaymentProvider.PaymentMethodKey, StringComparison.OrdinalIgnoreCase);

    public Task<PaymentProviderResult> ProcessAsync(
        PaymentProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (environment.IsDevelopment())
        {
            logger.LogWarning(
                "Immediate payment used in Development for method {PaymentMethod}. Prefer paymentMethod=development for test checkout.",
                request.PaymentMethod);
        }
        else
        {
            logger.LogWarning(
                "PSP not configured. Order marked paid via Immediate provider for method {PaymentMethod}.",
                request.PaymentMethod);
        }

        var transactionId = $"IMM-{Guid.NewGuid():N}".ToUpperInvariant();

        return Task.FromResult(new PaymentProviderResult(
            true,
            "Paid",
            transactionId,
            ProviderName));
    }
}
