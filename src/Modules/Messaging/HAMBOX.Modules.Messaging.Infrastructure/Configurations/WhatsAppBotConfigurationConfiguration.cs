using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Messaging.Infrastructure.Configurations;

internal sealed class WhatsAppBotConfigurationConfiguration : IEntityTypeConfiguration<WhatsAppBotConfiguration>
{
    public void Configure(EntityTypeBuilder<WhatsAppBotConfiguration> builder)
    {
        builder.ToTable("WhatsAppBotConfigurations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.WelcomeMessageEn).HasMaxLength(500).IsRequired();
        builder.Property(c => c.WelcomeMessageAr).HasMaxLength(500).IsRequired();
        builder.Property(c => c.FallbackMessageEn).HasMaxLength(500).IsRequired();
        builder.Property(c => c.FallbackMessageAr).HasMaxLength(500).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(64);
        builder.Property(c => c.ModifiedBy).HasMaxLength(64);
    }
}
