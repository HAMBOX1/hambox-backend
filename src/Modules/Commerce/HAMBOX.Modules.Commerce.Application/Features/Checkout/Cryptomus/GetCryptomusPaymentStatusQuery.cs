using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Cryptomus;

/// <summary>
/// Read-only status poll for the customer's payment-result page. Deliberately never triggers a
/// live Cryptomus call itself on every poll — it reports the last state the webhook or an earlier
/// active-poll re-verification already established (see <c>GetCryptomusPaymentStatusQueryHandler</c>
/// for when it does re-verify), mirroring <c>Dot.GetDotPaymentStatusQuery</c>.
/// </summary>
public sealed record GetCryptomusPaymentStatusQuery(Guid PaymentAttemptId) : IRequest<Result<CryptomusPaymentStatusDto>>;
