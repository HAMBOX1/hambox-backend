using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

/// <summary>
/// Configures the <see cref="UserNotification"/> entity for Entity Framework Core.
/// </summary>
internal sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Body)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(n => n.Category)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.ActionUrl)
            .HasMaxLength(500);

        builder.Property(n => n.IsRead)
            .IsRequired();

        builder.Property(n => n.IsArchived)
            .IsRequired();

        builder.Property(n => n.IsDeleted)
            .IsRequired();

        builder.Property(n => n.DeletedOnUtc);

        builder.Property(n => n.CreatedOnUtc)
            .IsRequired();

        builder.Property(n => n.ModifiedOnUtc);

        builder.HasIndex(n => n.UserId)
            .HasDatabaseName("IX_UserNotifications_UserId");

        builder.HasIndex(n => new { n.UserId, n.IsRead })
            .HasDatabaseName("IX_UserNotifications_UserId_IsRead");

        builder.HasIndex(n => new { n.UserId, n.IsArchived })
            .HasDatabaseName("IX_UserNotifications_UserId_IsArchived");
    }
}
