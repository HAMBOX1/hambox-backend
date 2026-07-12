using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout;

public sealed record GetCheckoutConfigurationQuery : IRequest<Result<CheckoutConfigurationDto>>;

public sealed record CheckoutConfigurationDto(bool DevelopmentCheckoutEnabled);

internal sealed class GetCheckoutConfigurationQueryHandler(ICheckoutConfigurationProvider configuration)
    : IRequestHandler<GetCheckoutConfigurationQuery, Result<CheckoutConfigurationDto>>
{
    public Task<Result<CheckoutConfigurationDto>> Handle(
        GetCheckoutConfigurationQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(
            new CheckoutConfigurationDto(configuration.IsDevelopmentCheckoutEnabled)));
}
