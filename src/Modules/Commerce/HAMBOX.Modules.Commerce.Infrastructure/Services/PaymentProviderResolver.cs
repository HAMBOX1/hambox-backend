using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class PaymentProviderResolver(IEnumerable<IPaymentProvider> providers)
{
    public Result<IPaymentProvider> Resolve(string paymentMethod)
    {
        var provider = providers.FirstOrDefault(p => p.CanHandle(paymentMethod));
        return provider is null
            ? Result.Failure<IPaymentProvider>(CommerceErrors.PaymentMethodNotSupported)
            : Result.Success(provider);
    }
}
