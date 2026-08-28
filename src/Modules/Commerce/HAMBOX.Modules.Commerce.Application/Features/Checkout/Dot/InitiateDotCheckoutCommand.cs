using HAMBOX.Application.Idempotency;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

/// <summary>
/// Initiates a DOT (carrier-billing OTP) checkout: validates and prices the cart exactly like
/// <see cref="CheckoutCommand"/>, but instead of charging synchronously, creates a Pending order +
/// Pending payment attempt and returns a redirect URL to DOT's OTP landing page. The order is only
/// ever completed later, by <c>VerifyAndFinalizeDotPaymentCommand</c>, after an authoritative
/// server-to-server verification — never by this command and never by the browser redirect alone.
/// </summary>
/// <param name="Wallet">
/// The customer's selected mobile wallet — must name one of <see cref="DotWalletOperator"/>'s
/// members ("OrangeCash", "VodafoneCash"), enforced by <c>InitiateDotCheckoutCommandValidator</c>.
/// Determines the <c>op_id</c> sent to DOT.
/// </param>
public sealed record InitiateDotCheckoutCommand(
    string Email,
    string Country,
    string Wallet,
    string IpAddress,
    string UserAgent,
    string Language)
    : IRequest<Result<DotCheckoutInitiationDto>>, IIdempotentRequest<DotCheckoutInitiationDto>;
