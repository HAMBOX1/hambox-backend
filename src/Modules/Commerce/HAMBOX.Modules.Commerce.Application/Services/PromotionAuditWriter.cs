using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Services;

internal static class PromotionAuditWriter
{
    public static void Write(
        ICommerceDbContext dbContext,
        Guid? promotionId,
        Guid? couponCodeId,
        PromotionAuditAction action,
        string? userId,
        string? details = null)
    {
        dbContext.PromotionAuditLogs.Add(PromotionAuditLog.Create(
            promotionId,
            couponCodeId,
            action,
            userId,
            details));
    }
}
