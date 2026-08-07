using HAMBOX.Modules.Commerce.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Persistence;

/// <summary>
/// Seeds default membership plans and demo tiers.
/// </summary>
public static class MembershipDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CommerceDbContext>>();

        if (await dbContext.MembershipPlans.AnyAsync(cancellationToken))
        {
            return;
        }

        var free = CreatePlan("Free", "free", "Starter access for all members.", 0m, 365, 0, "Free", null, true);
        free.AddBenefit(MembershipBenefitType.ReferralMultiplier, "1", "1× Referral Bonus", 1);
        free.AddBenefit(MembershipBenefitType.MaxPurchasesPerMonth, "5", "5 Purchases per Month", 2);
        free.Activate();

        var silver = CreatePlan("Silver", "silver", "Enhanced savings and support.", 9.99m, 30, 1, "Silver", null);
        silver.AddBenefit(MembershipBenefitType.DiscountPercentage, "5", "5% Store Discount", 1);
        silver.AddBenefit(MembershipBenefitType.ReferralMultiplier, "1.25", "1.25× Referral Bonus", 2);
        silver.AddBenefit(MembershipBenefitType.PrioritySupport, "true", "Priority Support", 3);
        silver.Activate();

        var gold = CreatePlan("Gold", "gold", "Popular plan with meaningful perks.", 19.99m, 30, 2, "Gold", null);
        gold.AddBenefit(MembershipBenefitType.DiscountPercentage, "10", "10% Store Discount", 1);
        gold.AddBenefit(MembershipBenefitType.ReferralMultiplier, "1.5", "1.5× Referral Bonus", 2);
        gold.AddBenefit(MembershipBenefitType.EarlyAccess, "3", "3-Day Early Access", 3);
        gold.AddBenefit(MembershipBenefitType.PrioritySupport, "true", "Priority Support", 4);
        gold.Activate();

        var platinum = CreatePlan("Platinum", "platinum", "Premium marketplace access.", 39.99m, 30, 3, "Platinum", null);
        platinum.AddBenefit(MembershipBenefitType.DiscountPercentage, "15", "15% Store Discount", 1);
        platinum.AddBenefit(MembershipBenefitType.ReferralMultiplier, "1.75", "1.75× Referral Bonus", 2);
        platinum.AddBenefit(MembershipBenefitType.ExclusiveProducts, "true", "Exclusive Marketplace", 3);
        platinum.AddBenefit(MembershipBenefitType.EarlyAccess, "5", "5-Day Early Access", 4);
        platinum.Activate();

        var vip = CreatePlan("VIP", "vip", "Maximum benefits for top members.", 79.99m, 30, 4, "VIP", null);
        vip.AddBenefit(MembershipBenefitType.DiscountPercentage, "20", "20% Store Discount", 1);
        vip.AddBenefit(MembershipBenefitType.ReferralMultiplier, "2", "2× Referral Bonus", 2);
        vip.AddBenefit(MembershipBenefitType.ExclusiveProducts, "true", "Exclusive Marketplace", 3);
        vip.AddBenefit(MembershipBenefitType.EarlyAccess, "7", "7-Day Early Access", 4);
        vip.AddBenefit(MembershipBenefitType.ThemeUnlock, "true", "VIP Theme Unlock", 5);
        vip.Activate();

        dbContext.MembershipPlans.AddRange(free, silver, gold, platinum, vip);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded membership plans (Free, Silver, Gold, Platinum, VIP).");
    }

    private static MembershipPlan CreatePlan(
        string name,
        string slug,
        string description,
        decimal price,
        int durationDays,
        int sortOrder,
        string badge,
        string? themeKey,
        bool isDefault = false) =>
        MembershipPlan.Create(name, slug, description, price, durationDays, sortOrder, badge, themeKey, isDefault);
}
