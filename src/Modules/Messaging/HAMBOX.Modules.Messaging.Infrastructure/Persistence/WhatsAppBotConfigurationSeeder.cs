using HAMBOX.Modules.Messaging.Application.Services;
using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Messaging.Infrastructure.Persistence;

/// <summary>Seeds the one <see cref="WhatsAppBotConfiguration"/> row and the fixed seven
/// <see cref="WhatsAppMenuItem"/> rows from <see cref="WhatsAppBotConfigurationDefaults"/> — the exact
/// text the bot used before this feature existed, so seeding changes nothing about what a fresh
/// deployment's bot says. Idempotent — a no-op once the configuration row exists.</summary>
public static class WhatsAppBotConfigurationSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();

        if (await db.WhatsAppBotConfigurations.AnyAsync())
        {
            return;
        }

        db.WhatsAppBotConfigurations.Add(WhatsAppBotConfiguration.CreateDefault(
            WhatsAppBotConfigurationDefaults.WelcomeMessageEn,
            WhatsAppBotConfigurationDefaults.WelcomeMessageAr,
            WhatsAppBotConfigurationDefaults.FallbackMessageEn,
            WhatsAppBotConfigurationDefaults.FallbackMessageAr));

        var sortOrder = 0;
        foreach (var (action, labelEn, labelAr) in WhatsAppBotConfigurationDefaults.Items)
        {
            db.WhatsAppMenuItems.Add(WhatsAppMenuItem.CreateDefault(action, sortOrder, labelEn, labelAr));
            sortOrder++;
        }

        await db.SaveChangesAsync();
    }
}
