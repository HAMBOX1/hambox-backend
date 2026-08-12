using HAMBOX.Modules.Commerce.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

internal sealed class FakePaymentProvider : IPaymentProvider
{
    public string ProviderName => "development";

    public bool CanHandle(string paymentMethod) => true;

    public Task<PaymentProviderResult> ProcessAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentProviderResult(true, "Succeeded", $"txn-{Guid.NewGuid():N}", ProviderName));
}
