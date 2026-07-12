using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Persistence;

/// <summary>
/// Seeds default promotions when the database has none.
/// </summary>
public static class PromotionDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CommerceDbContext>>();

        if (await dbContext.Promotions.AnyAsync(cancellationToken))
        {
            return;
        }

        // Membership discounts are resolved from plan benefits via MembershipEngine → PromotionEngine.
        // Seed a generic first-purchase promotion instead of a duplicate membership discount.
        var welcome = Promotion.Create(
            "Welcome Offer",
            "5% off your first purchase when signed in.",
            PromotionType.Automatic,
            DiscountType.Percentage,
            5m);
        welcome.Publish();

        dbContext.Promotions.Add(welcome);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded default welcome promotion.");
    }
}
