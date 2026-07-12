using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Domain.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>
{
    public void Configure(EntityTypeBuilder<PermissionGroup> builder)
    {
        builder.ToTable("PermissionGroups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Key).IsRequired().HasMaxLength(100);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
        builder.Property(g => g.Module).IsRequired().HasMaxLength(100);
        builder.Property(g => g.SortOrder).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(500);
        builder.Property(g => g.CreatedOnUtc).IsRequired();
        builder.Property(g => g.ModifiedOnUtc);

        builder.HasIndex(g => g.Key).IsUnique().HasDatabaseName("IX_PermissionGroups_Key");

        var utcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(PermissionDefinitionRegistry.Groups.Select(g => new
        {
            g.Id,
            g.Key,
            g.Name,
            g.Module,
            g.SortOrder,
            g.Description,
            CreatedOnUtc = utcNow,
        }));
    }
}
