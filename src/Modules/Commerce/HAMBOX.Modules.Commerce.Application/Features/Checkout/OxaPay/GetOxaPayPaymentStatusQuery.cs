using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.OxaPay;

/// <summary>
/// Read-only status poll for the customer's payment-result page. Deliberately never triggers a live
/// OxaPay call itself on every poll — it reports the last state the webhook or an earlier active-poll
/// re-verification already established (see <c>GetOxaPayPaymentStatusQueryHandler</c> for when it
/// does re-verify), mirroring <c>Cryptomus.GetCryptomusPaymentStatusQuery</c>.
/// </summary>
public sealed record GetOxaPayPaymentStatusQuery(Guid PaymentAttemptId) : IRequest<Result<OxaPayPaymentStatusDto>>;
