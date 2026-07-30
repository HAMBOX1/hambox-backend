using HAMBOX.Modules.Support.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Support.Infrastructure.Persistence;

/// <summary>Seeds the default categories/priorities every ticket needs a fallback for, and the
/// Support Communication templates. Idempotent — a no-op once any row exists.</summary>
public static class SupportDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();

        if (!await db.TicketCategories.AnyAsync())
        {
            db.TicketCategories.Add(TicketCategory.Create("General", "#6366F1", "pi pi-comments", 1, isDefault: true));
            db.TicketCategories.Add(TicketCategory.Create("Billing", "#F59E0B", "pi pi-credit-card", 2));
            db.TicketCategories.Add(TicketCategory.Create("Technical", "#EF4444", "pi pi-wrench", 3));
            db.TicketCategories.Add(TicketCategory.Create("Account", "#3B82F6", "pi pi-user", 4));
            db.TicketCategories.Add(TicketCategory.Create("Orders", "#10B981", "pi pi-shopping-cart", 5));
        }

        if (!await db.TicketPriorities.AnyAsync())
        {
            db.TicketPriorities.Add(TicketPriority.Create("Low", "#6B7280", 1, 1440, 4320));
            db.TicketPriorities.Add(TicketPriority.Create("Normal", "#3B82F6", 2, 480, 1440, isDefault: true));
            db.TicketPriorities.Add(TicketPriority.Create("High", "#F59E0B", 3, 120, 480));
            db.TicketPriorities.Add(TicketPriority.Create("Urgent", "#EF4444", 4, 30, 240));
        }

        await db.SaveChangesAsync();

        await SupportCommunicationTemplateSeeder.SeedAsync(services);
    }
}
