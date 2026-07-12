using HAMBOX.Modules.Catalog.Domain.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

internal sealed class SearchQueryLogConfiguration : IEntityTypeConfiguration<SearchQueryLog>
{
    public void Configure(EntityTypeBuilder<SearchQueryLog> builder)
    {
        builder.ToTable("SearchQueryLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Query)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.ResultCount)
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasMaxLength(450);

        builder.Property(x => x.Ip)
            .HasMaxLength(64);

        builder.Property(x => x.CreatedOnUtc)
            .IsRequired();

        builder.Property(x => x.ModifiedOnUtc);

        builder.HasIndex(x => x.CreatedOnUtc)
            .HasDatabaseName("IX_SearchQueryLogs_CreatedOnUtc");

        builder.HasIndex(x => x.Query)
            .HasDatabaseName("IX_SearchQueryLogs_Query");
    }
}
