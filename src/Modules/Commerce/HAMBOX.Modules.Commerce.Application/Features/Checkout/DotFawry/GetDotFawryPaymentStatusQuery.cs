using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;

/// <summary>
/// Read-only status poll for the customer's Fawry payment-result page. Deliberately never triggers
/// a live DOT call itself — it reports the last state the notification webhook or the
/// reconciliation sweep already established, so a tight polling loop from the frontend can't hammer
/// DOT's API or race the same attempt through verification twice.
/// </summary>
public sealed record GetDotFawryPaymentStatusQuery(Guid PaymentAttemptId) : IRequest<Result<DotFawryPaymentStatusDto>>;
