using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout;

public sealed record CheckoutCommand(string Email, string Country, string PaymentMethod)
    : IRequest<Result<Contracts.OrderDto>>;
