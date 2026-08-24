using HAMBOX.Modules.Commerce.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Temporary provider that completes checkout without an external PSP.
/// Development-only: no real PSP is integrated for the generic checkout path, so outside
/// Development this must NEVER match — otherwise any unrecognized paymentMethod string would
/// silently mark an order paid for $0 in Production (see CheckoutCommandHandler's
/// PaymentMethodNotSupported fallback, which is what should fire instead).
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
        logger.LogWarning(
            "Immediate payment used in Development for method {PaymentMethod}. Prefer paymentMethod=development for test checkout.",
            request.PaymentMethod);

        var transactionId = $"IMM-{Guid.NewGuid():N}".ToUpperInvariant();

        return Task.FromResult(new PaymentProviderResult(
            true,
            "Paid",
            transactionId,
            ProviderName));
    }
}
