using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.GetAdminOrderById;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.OrderHold;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.ResendAdminOrderCodes;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.UpdateAdminOrderStatus;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.BulkAdminOrders;

public sealed record BulkAdminOrdersCommand(BulkAdminOrdersRequest Request)
    : IRequest<Result<BulkAdminOrdersResultDto>>;

internal sealed class BulkAdminOrdersCommandHandler
    : IRequestHandler<BulkAdminOrdersCommand, Result<BulkAdminOrdersResultDto>>
{
    private readonly ISender _sender;

    public BulkAdminOrdersCommandHandler(ISender sender) => _sender = sender;

    public async Task<Result<BulkAdminOrdersResultDto>> Handle(
        BulkAdminOrdersCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Request.OrderIds.Count == 0)
        {
            return Result.Failure<BulkAdminOrdersResultDto>(CommerceErrors.OrderBulkEmpty);
        }

        var action = command.Request.Action.Trim().ToLowerInvariant();

        // Everything but a plain re-send is a manual action on someone's order: it needs a recorded reason.
        var reason = action == "resend-codes" ? Result.Success(string.Empty) : AdminActionReason.Require(command.Request.Reason);
        if (reason.IsFailure)
        {
            return Result.Failure<BulkAdminOrdersResultDto>(reason.Error);
        }

        var success = 0;
        var errors = new List<string>();

        foreach (var orderId in command.Request.OrderIds.Distinct())
        {
            var result = action switch
            {
                "cancel" => await BulkStatusAsync(orderId, "Cancelled", reason.Value, cancellationToken),
                "mark-processing" => await BulkStatusAsync(orderId, "Processing", reason.Value, cancellationToken),
                "mark-completed" => await BulkStatusAsync(orderId, "Completed", reason.Value, cancellationToken),
                "hold" => await _sender.Send(new HoldAdminOrderCommand(orderId, reason.Value), cancellationToken),
                "resend-codes" => await BulkResendAsync(orderId, cancellationToken),
                _ => Result.Failure(CommerceErrors.OrderBulkActionNotSupported(action)),
            };

            if (result.IsSuccess)
            {
                success++;
            }
            else
            {
                errors.Add($"{orderId}: {result.Error.Description}");
            }
        }

        return Result.Success(new BulkAdminOrdersResultDto(
            success,
            errors.Count,
            errors));
    }

    private async Task<Result> BulkStatusAsync(
        Guid orderId,
        string status,
        string reason,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateAdminOrderStatusCommand(orderId, new UpdateAdminOrderStatusRequest(status, reason)),
            cancellationToken);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    private Task<Result> BulkResendAsync(Guid orderId, CancellationToken cancellationToken) =>
        _sender.Send(new ResendAdminOrderCodesCommand(orderId), cancellationToken);
}
