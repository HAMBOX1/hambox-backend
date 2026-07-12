using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Domain.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.GroupId).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.NormalizedName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.SortOrder).IsRequired();
        builder.Property(p => p.CreatedOnUtc).IsRequired();
        builder.Property(p => p.ModifiedOnUtc);

        builder.HasIndex(p => p.NormalizedName).IsUnique().HasDatabaseName("IX_Permissions_NormalizedName");
        builder.HasIndex(p => p.GroupId).HasDatabaseName("IX_Permissions_GroupId");

        var utcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(PermissionDefinitionRegistry.Permissions.Select(p => new
        {
            p.Id,
            p.GroupId,
            p.Name,
            NormalizedName = p.Name.ToUpperInvariant(),
            p.Description,
            p.SortOrder,
            CreatedOnUtc = utcNow,
        }));
    }
}
