using HAMBOX.Application.Idempotency;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.OxaPay;

/// <summary>
/// Initiates an OxaPay (crypto) checkout: validates and prices the cart exactly like
/// <see cref="Cryptomus.InitiateCryptomusCheckoutCommand"/>'s Cryptomus equivalent, but instead of
/// charging synchronously, creates a Pending order + Pending payment attempt and returns a redirect
/// URL to OxaPay's hosted invoice page. The order is only ever completed later, by
/// <c>OxaPayPaymentVerificationService</c>, after an authoritative server-to-server verification —
/// never by this command and never by the browser redirect alone.
/// </summary>
public sealed record InitiateOxaPayCheckoutCommand(
    string Email,
    string Country,
    string IpAddress,
    string UserAgent,
    string Language)
    : IRequest<Result<OxaPayCheckoutInitiationDto>>, IIdempotentRequest<OxaPayCheckoutInitiationDto>;
