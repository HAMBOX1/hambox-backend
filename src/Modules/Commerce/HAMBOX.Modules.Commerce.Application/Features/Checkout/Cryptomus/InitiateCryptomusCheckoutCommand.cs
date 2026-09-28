using HAMBOX.Application.Idempotency;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Cryptomus;

/// <summary>
/// Initiates a Cryptomus (crypto/USDT) checkout: validates and prices the cart exactly like
/// <see cref="Dot.InitiateDotCheckoutCommand"/>'s DOT equivalent, but instead of charging
/// synchronously, creates a Pending order + Pending payment attempt and returns a redirect URL to
/// Cryptomus's hosted invoice page. The order is only ever completed later, by
/// <c>CryptomusPaymentVerificationService</c>, after an authoritative server-to-server
/// verification — never by this command and never by the browser redirect alone.
/// </summary>
public sealed record InitiateCryptomusCheckoutCommand(
    string Email,
    string Country,
    string IpAddress,
    string UserAgent,
    string Language)
    : IRequest<Result<CryptomusCheckoutInitiationDto>>, IIdempotentRequest<CryptomusCheckoutInitiationDto>;
