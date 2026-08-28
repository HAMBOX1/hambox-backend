using HAMBOX.Application.Idempotency;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout;

public sealed record CheckoutCommand(
    string Email,
    string Country,
    string PaymentMethod,
    string IpAddress,
    string UserAgent,
    string Language)
    : IRequest<Result<Contracts.OrderDto>>, IIdempotentRequest<Contracts.OrderDto>;
