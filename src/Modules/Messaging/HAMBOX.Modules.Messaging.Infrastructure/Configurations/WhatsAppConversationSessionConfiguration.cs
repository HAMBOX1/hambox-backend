using HAMBOX.Modules.Messaging.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Messaging.Infrastructure.Configurations;

internal sealed class WhatsAppConversationSessionConfiguration : IEntityTypeConfiguration<WhatsAppConversationSession>
{
    public void Configure(EntityTypeBuilder<WhatsAppConversationSession> builder)
    {
        builder.ToTable("WhatsAppConversationSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.PhoneNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(s => s.PhoneNumber).IsUnique();

        builder.Property(s => s.CurrentMenu).HasMaxLength(64).IsRequired();
        builder.Property(s => s.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(s => s.ContextJson).HasMaxLength(2000);
        builder.Property(s => s.CustomerUserId).HasMaxLength(64);
        builder.Property(s => s.PendingVerificationEmail).HasMaxLength(256);
        builder.Property(s => s.PendingVerificationCodeHash).HasMaxLength(128);
    }
}
