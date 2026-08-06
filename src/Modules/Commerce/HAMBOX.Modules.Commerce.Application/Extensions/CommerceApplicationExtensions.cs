using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Promotions;
using HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;
using HAMBOX.Modules.Commerce.Application.Services;
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
        services.AddScoped<MembershipOperationsService>();
        services.AddScoped<ReferralRewardService>();
        services.AddScoped<OrderFulfillmentService>();
        services.AddScoped<OrderInventoryReleaseService>();
        services.AddScoped<IPromotionTypeEvaluator, AutomaticPromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, MembershipPromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, CategoryPromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, ProductPromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, FlashSalePromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, ReferralRewardPromotionEvaluator>();
        services.AddScoped<IPromotionTypeEvaluator, CouponPromotionEvaluator>();
        services.AddScoped<CartResponseBuilder>();

        return services;
    }
}
