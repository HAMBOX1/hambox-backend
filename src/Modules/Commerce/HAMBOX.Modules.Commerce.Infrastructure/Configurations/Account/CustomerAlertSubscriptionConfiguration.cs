using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

/// <summary>
/// Configures the <see cref="CustomerAlertSubscription"/> entity for Entity Framework Core.
/// </summary>
internal sealed class CustomerAlertSubscriptionConfiguration : IEntityTypeConfiguration<CustomerAlertSubscription>
{
    public void Configure(EntityTypeBuilder<CustomerAlertSubscription> builder)
    {
        builder.ToTable("CustomerAlertSubscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId).HasMaxLength(450);
        builder.Property(s => s.GuestSessionId).HasMaxLength(100);
        builder.Property(s => s.AlertType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.VariantId).IsRequired();
        builder.Property(s => s.ProductId).IsRequired();
        builder.Property(s => s.LastObservedPrice).HasColumnType("decimal(18,2)");
        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.CreatedOnUtc).IsRequired();

        // Filtered so only one ACTIVE subscription per (owner, variant, type) can exist at a time —
        // MarkNotified() flips IsActive false, which frees the row up for a fresh subscribe. Same
        // pattern as the production ProductVariants.Sku fix: uniqueness scoped to the live subset,
        // not the whole table.
        builder.HasIndex(s => new { s.UserId, s.VariantId, s.AlertType })
            .IsUnique()
            .HasDatabaseName("IX_CustomerAlertSubscriptions_UserId_VariantId_AlertType")
            .HasFilter("[UserId] IS NOT NULL AND [IsActive] = 1");

        builder.HasIndex(s => new { s.GuestSessionId, s.VariantId, s.AlertType })
            .IsUnique()
            .HasDatabaseName("IX_CustomerAlertSubscriptions_GuestSessionId_VariantId_AlertType")
            .HasFilter("[GuestSessionId] IS NOT NULL AND [IsActive] = 1");

        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_CustomerAlertSubscriptions_UserId");

        // Scan-job selection query: "distinct active variants of type X" — see the job handlers.
        builder.HasIndex(s => new { s.AlertType, s.VariantId })
            .HasDatabaseName("IX_CustomerAlertSubscriptions_AlertType_VariantId")
            .HasFilter("[IsActive] = 1");
    }
}
