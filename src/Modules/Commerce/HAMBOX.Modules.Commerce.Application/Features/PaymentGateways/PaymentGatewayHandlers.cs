using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.PaymentGateways;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.PaymentGateways;

public sealed record GetPaymentGatewaysQuery : IRequest<Result<IReadOnlyList<PaymentGatewayListItemDto>>>;

internal sealed class GetPaymentGatewaysQueryHandler(ICommerceDbContext dbContext)
    : IRequestHandler<GetPaymentGatewaysQuery, Result<IReadOnlyList<PaymentGatewayListItemDto>>>
{
    public async Task<Result<IReadOnlyList<PaymentGatewayListItemDto>>> Handle(GetPaymentGatewaysQuery request, CancellationToken cancellationToken)
    {
        var items = await dbContext.PaymentGatewayConfigurations
            .AsNoTracking()
            .OrderBy(g => g.DisplayName)
            .Select(g => new PaymentGatewayListItemDto(
                g.GatewayKey, g.DisplayName, g.IsEnabled, g.IsTestMode,
                !string.IsNullOrEmpty(g.ApiKey), !string.IsNullOrEmpty(g.ApiSecret)))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PaymentGatewayListItemDto>>(items);
    }
}

public sealed record GetPaymentGatewayByKeyQuery(string GatewayKey) : IRequest<Result<PaymentGatewayDetailDto>>;

internal sealed class GetPaymentGatewayByKeyQueryHandler(ICommerceDbContext dbContext)
    : IRequestHandler<GetPaymentGatewayByKeyQuery, Result<PaymentGatewayDetailDto>>
{
    public async Task<Result<PaymentGatewayDetailDto>> Handle(GetPaymentGatewayByKeyQuery request, CancellationToken cancellationToken)
    {
        var gateway = await dbContext.PaymentGatewayConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.GatewayKey == request.GatewayKey, cancellationToken);

        return gateway is null
            ? Result.Failure<PaymentGatewayDetailDto>(CommerceErrors.PaymentGatewayNotFound)
            : Result.Success(ToDetail(gateway));
    }

    internal static PaymentGatewayDetailDto ToDetail(PaymentGatewayConfiguration g) => new(
        g.GatewayKey, g.DisplayName, g.IsEnabled, g.IsTestMode, g.FeePercent, g.BaseUrl, g.AccountId, g.SecondaryId,
        !string.IsNullOrEmpty(g.ApiKey), !string.IsNullOrEmpty(g.ApiSecret),
        g.WebhookUrl, g.FrontendResultUrl, g.AdditionalConfigJson, g.ModifiedOnUtc);
}

public sealed record UpdatePaymentGatewayGeneralCommand(string GatewayKey, UpdatePaymentGatewayGeneralRequest Request) : IRequest<Result>;

internal sealed class UpdatePaymentGatewayGeneralCommandHandler(
    ICommerceDbContext dbContext,
    ICurrentUserService currentUser,
    IPaymentGatewayConfigurationProvider settingsProvider) : IRequestHandler<UpdatePaymentGatewayGeneralCommand, Result>
{
    public async Task<Result> Handle(UpdatePaymentGatewayGeneralCommand request, CancellationToken cancellationToken)
    {
        var gateway = await dbContext.PaymentGatewayConfigurations
            .FirstOrDefaultAsync(g => g.GatewayKey == request.GatewayKey, cancellationToken);
        if (gateway is null)
        {
            return Result.Failure(CommerceErrors.PaymentGatewayNotFound);
        }

        var r = request.Request;
        gateway.UpdateGeneral(r.DisplayName, r.IsTestMode, r.FeePercent, r.BaseUrl, r.AccountId, r.WebhookUrl, r.FrontendResultUrl, r.AdditionalConfigJson, CurrentUserId(currentUser));
        await dbContext.SaveChangesAsync(cancellationToken);
        settingsProvider.InvalidateCache(request.GatewayKey);

        return Result.Success();
    }

    internal static Guid? CurrentUserId(ICurrentUserService currentUser) =>
        Guid.TryParse(currentUser.UserId, out var id) ? id : null;
}

public sealed record UpdatePaymentGatewayCredentialsCommand(string GatewayKey, UpdatePaymentGatewayCredentialsRequest Request) : IRequest<Result>;

internal sealed class UpdatePaymentGatewayCredentialsCommandHandler(
    ICommerceDbContext dbContext,
    ICurrentUserService currentUser,
    IPaymentGatewayConfigurationProvider settingsProvider) : IRequestHandler<UpdatePaymentGatewayCredentialsCommand, Result>
{
    public async Task<Result> Handle(UpdatePaymentGatewayCredentialsCommand request, CancellationToken cancellationToken)
    {
        var gateway = await dbContext.PaymentGatewayConfigurations
            .FirstOrDefaultAsync(g => g.GatewayKey == request.GatewayKey, cancellationToken);
        if (gateway is null)
        {
            return Result.Failure(CommerceErrors.PaymentGatewayNotFound);
        }

        var r = request.Request;
        gateway.UpdateCredentials(r.ApiKey, r.ApiSecret, r.SecondaryId, UpdatePaymentGatewayGeneralCommandHandler.CurrentUserId(currentUser));
        await dbContext.SaveChangesAsync(cancellationToken);
        settingsProvider.InvalidateCache(request.GatewayKey);

        return Result.Success();
    }
}

public sealed record SetPaymentGatewayEnabledCommand(string GatewayKey, bool IsEnabled) : IRequest<Result>;

internal sealed class SetPaymentGatewayEnabledCommandHandler(
    ICommerceDbContext dbContext,
    ICurrentUserService currentUser,
    IPaymentGatewayConfigurationProvider settingsProvider) : IRequestHandler<SetPaymentGatewayEnabledCommand, Result>
{
    public async Task<Result> Handle(SetPaymentGatewayEnabledCommand request, CancellationToken cancellationToken)
    {
        var gateway = await dbContext.PaymentGatewayConfigurations
            .FirstOrDefaultAsync(g => g.GatewayKey == request.GatewayKey, cancellationToken);
        if (gateway is null)
        {
            return Result.Failure(CommerceErrors.PaymentGatewayNotFound);
        }

        var userId = UpdatePaymentGatewayGeneralCommandHandler.CurrentUserId(currentUser);
        if (request.IsEnabled)
        {
            gateway.Enable(userId);
        }
        else
        {
            gateway.Disable(userId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        settingsProvider.InvalidateCache(request.GatewayKey);

        return Result.Success();
    }
}

public sealed record TestPaymentGatewayConnectionCommand(string GatewayKey) : IRequest<Result<PaymentGatewayTestConnectionResultDto>>;

internal sealed class TestPaymentGatewayConnectionCommandHandler(
    ICommerceDbContext dbContext,
    IEnumerable<IPaymentGateway> gateways) : IRequestHandler<TestPaymentGatewayConnectionCommand, Result<PaymentGatewayTestConnectionResultDto>>
{
    public async Task<Result<PaymentGatewayTestConnectionResultDto>> Handle(TestPaymentGatewayConnectionCommand request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.PaymentGatewayConfigurations
            .AsNoTracking()
            .AnyAsync(g => g.GatewayKey == request.GatewayKey, cancellationToken);
        if (!exists)
        {
            return Result.Failure<PaymentGatewayTestConnectionResultDto>(CommerceErrors.PaymentGatewayNotFound);
        }

        var gateway = gateways.FirstOrDefault(g => g.GatewayKey == request.GatewayKey);
        if (gateway is null)
        {
            return Result.Success(new PaymentGatewayTestConnectionResultDto(false, "No integration is registered for this gateway yet."));
        }

        var result = await gateway.TestConnectionAsync(cancellationToken);
        return Result.Success(new PaymentGatewayTestConnectionResultDto(result.IsSuccess, result.Message));
    }
}
