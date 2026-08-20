using HAMBOX.Application.Fulfillment;
using HAMBOX.Application.Membership;
using HAMBOX.Application.Referrals;
using HAMBOX.Application.Variants;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Promotions;
using HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Application.Variants;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Commerce.Application.Extensions;

/// <summary>
/// Extension methods for registering Commerce application services.
/// </summary>
public static class CommerceApplicationExtensions
{
    public static IServiceCollection AddCommerceApplication(this IServiceCollection services)
    {
        services.AddScoped<IPromotionEngine, PromotionEngine>();
        services.AddScoped<IMembershipEngine, MembershipEngine>();
        services.AddScoped<IMembershipAccessProvider, MembershipAccessProvider>();
        services.AddScoped<ICommerceVariantUsageProvider, CommerceVariantUsageProvider>();
        services.AddScoped<MembershipOperationsService>();
        services.AddScoped<ReferralRewardService>();
        services.AddScoped<ReferralLifecycleService>();
        services.AddScoped<IReferralRedemptionService>(sp => sp.GetRequiredService<ReferralLifecycleService>());
        services.AddScoped<OrderFulfillmentService>();
        services.AddScoped<OrderInventoryReleaseService>();
        services.AddScoped<IPromotionTypeEvaluator, AutomaticPromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, MembershipPromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, CategoryPromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, ProductPromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, FlashSalePromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, CouponPromotionEvaluator>();
        services.AddScoped<CartResponseBuilder>();
        services.AddScoped<CartLineValidator>();
        services.AddScoped<PromotionRedemptionService>();
        services.AddScoped<DotPaymentVerificationService>();
        services.AddScoped<DotFawryPaymentVerificationService>();
        services.AddScoped<ISupplierFulfillmentDeliverySink, CommerceOrderLicenseKeyDeliverySink>();
        services.AddScoped<IFulfillmentRouter, FulfillmentRouter>();

        return services;
    }
}
