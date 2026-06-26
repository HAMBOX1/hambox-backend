using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<Contracts.Account.OrderDetailDto>>;
