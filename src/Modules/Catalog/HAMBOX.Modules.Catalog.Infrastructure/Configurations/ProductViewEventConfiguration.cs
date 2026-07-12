using HAMBOX.Modules.Catalog.Domain.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

internal sealed class ProductViewEventConfiguration : IEntityTypeConfiguration<ProductViewEvent>
{
    public void Configure(EntityTypeBuilder<ProductViewEvent> builder)
    {
        builder.ToTable("ProductViewEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasMaxLength(450);

        builder.Property(x => x.CreatedOnUtc)
            .IsRequired();

        builder.Property(x => x.ModifiedOnUtc);

        builder.HasIndex(x => x.CreatedOnUtc)
            .HasDatabaseName("IX_ProductViewEvents_CreatedOnUtc");

        builder.HasIndex(x => x.ProductId)
            .HasDatabaseName("IX_ProductViewEvents_ProductId");
    }
}
