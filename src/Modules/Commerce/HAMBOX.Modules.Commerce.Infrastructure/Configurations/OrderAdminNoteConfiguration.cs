using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

internal sealed class OrderAdminNoteConfiguration : IEntityTypeConfiguration<OrderAdminNote>
{
    public void Configure(EntityTypeBuilder<OrderAdminNote> builder)
    {
        builder.ToTable("OrderAdminNotes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Body).HasMaxLength(4000).IsRequired();
        builder.Property(n => n.AuthorUserId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.AuthorDisplayName).HasMaxLength(200).IsRequired();

        builder.HasIndex(n => n.OrderId).HasDatabaseName("IX_OrderAdminNotes_OrderId");
    }
}
