using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Messaging.Infrastructure.Configurations;

internal sealed class WhatsAppMenuItemConfiguration : IEntityTypeConfiguration<WhatsAppMenuItem>
{
    public void Configure(EntityTypeBuilder<WhatsAppMenuItem> builder)
    {
        builder.ToTable("WhatsAppMenuItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(i => i.Action).IsUnique();

        builder.Property(i => i.LabelEn).HasMaxLength(60).IsRequired();
        builder.Property(i => i.LabelAr).HasMaxLength(60).IsRequired();
        builder.Property(i => i.CreatedBy).HasMaxLength(64);
        builder.Property(i => i.ModifiedBy).HasMaxLength(64);
    }
}
