using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(rp => rp.Id);

        builder.Property(rp => rp.RoleId).IsRequired();
        builder.Property(rp => rp.PermissionId).IsRequired();
        builder.Property(rp => rp.CreatedOnUtc).IsRequired();
        builder.Property(rp => rp.ModifiedOnUtc);

        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId })
            .IsUnique()
            .HasDatabaseName("IX_RolePermissions_RoleId_PermissionId");

        builder.HasOne<ApplicationRole>()
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        var utcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var index = 0;
        var seedData = RoleDefinitionRegistry.AdministratorPermissionIds.Select(permissionId =>
        {
            index++;
            return (object)new
            {
                Id = new Guid($"30000000-0000-0000-0000-{index:D12}"),
                RoleId = RoleDefinitionRegistry.RoleIds.Administrator,
                PermissionId = permissionId,
                CreatedOnUtc = utcNow,
            };
        }).ToArray();

        builder.HasData(seedData);
    }
}
