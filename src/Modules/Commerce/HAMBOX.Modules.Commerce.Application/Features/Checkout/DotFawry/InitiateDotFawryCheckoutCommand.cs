using HAMBOX.Application.Idempotency;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;

/// <summary>
/// Initiates a DOT Fawry Direct Billing checkout: validates and prices the cart exactly like the
/// standard synchronous checkout command, then calls DOT's server-to-server Direct Billing API and
/// creates a Pending order + Pending payment attempt. Unlike the carrier-billing OTP flow, there is
/// no browser redirect — DOT/Fawry SMS the customer a reference number, and the order is only ever
/// completed later, by <c>DotFawryPaymentVerificationService</c>, after an authoritative
/// server-to-server verification triggered by DOT's notification webhook or the reconciliation
/// sweep. A Direct Billing response of resultCode "1000" ("the transaction is being processed") is
/// never treated as payment success — see the module's <c>docs/integrations/dot/README.md</c>.
/// </summary>
/// <param name="Wallet">
/// The customer's selected mobile wallet — must name one of <see cref="DotFawryWalletOperator"/>'s
/// members ("Fawry", "OrangeCash", "VodafoneCash"), enforced by
/// <c>InitiateDotFawryCheckoutCommandValidator</c>. Determines the <c>opId</c> sent to DOT; every
/// other field (credentials, request shape) is identical across wallets.
/// </param>
public sealed record InitiateDotFawryCheckoutCommand(
    string Email,
    string Country,
    string PhoneNumber,
    string? CustomerName,
    string Wallet,
    string IpAddress,
    string UserAgent,
    string Language)
    : IRequest<Result<DotFawryCheckoutInitiationDto>>, IIdempotentRequest<DotFawryCheckoutInitiationDto>;
