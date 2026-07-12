using System.Text.Json;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Orders;

namespace HAMBOX.Modules.Commerce.Application.Services;

public static class OrderPaymentCallbackRecorder
{
    public static void Record(
        ICommerceDbContext dbContext,
        Guid orderId,
        PaymentProviderResult result,
        string eventType = "PaymentCaptured")
    {
        var payload = JsonSerializer.Serialize(new
        {
            result.IsSuccess,
            result.PaymentStatus,
            result.TransactionId,
            result.Provider,
            result.FailureReason,
        });

        dbContext.OrderPaymentCallbacks.Add(OrderPaymentCallback.Create(
            orderId,
            result.Provider,
            eventType,
            result.PaymentStatus,
            result.TransactionId,
            payload));
    }
}
