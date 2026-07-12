using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="Order"/> entity for Entity Framework Core.
/// </summary>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(o => o.Country)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.PaymentMethod)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.PaymentProvider)
            .HasMaxLength(50);

        builder.Property(o => o.PaymentTransactionId)
            .HasMaxLength(100);

        builder.Property(o => o.PaymentStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.Subtotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(o => o.DiscountAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(o => o.TaxAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(o => o.TotalAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(o => o.Kind)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.MembershipAction)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.MembershipPlanId);
        builder.Property(o => o.MembershipSubscriptionId);

        builder.Property(o => o.CreatedOnUtc)
            .IsRequired();

        builder.Property(o => o.ModifiedOnUtc);

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Order.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique()
            .HasDatabaseName("IX_Orders_OrderNumber");

        builder.HasIndex(o => o.UserId)
            .HasDatabaseName("IX_Orders_UserId");

        builder.Ignore(o => o.DomainEvents);
    }
}
