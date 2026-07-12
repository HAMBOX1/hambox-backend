using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.NormalizedName).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.IsDefault).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.IsSystem).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.PriorityLevel).IsRequired().HasDefaultValue(500);
        builder.Property(r => r.CreatedOnUtc).IsRequired();
        builder.Property(r => r.ModifiedOnUtc);

        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasIndex(r => r.NormalizedName).IsUnique().HasDatabaseName("IX_Roles_NormalizedName");

        builder.HasMany(r => r.RolePermissions)
            .WithOne()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.RolePermissions).HasField("_rolePermissions");

        builder.Ignore(r => r.DomainEvents);

        SeedRoles(builder);
    }

    private static void SeedRoles(EntityTypeBuilder<ApplicationRole> builder)
    {
        var utcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            new
            {
                Id = RoleDefinitionRegistry.RoleIds.Owner,
                Name = RoleConstants.Owner,
                NormalizedName = RoleConstants.Owner.ToUpperInvariant(),
                Description = "Full system access. Cannot be deleted.",
                IsDefault = false,
                IsSystem = true,
                PriorityLevel = 0,
                CreatedOnUtc = utcNow,
            },
            new
            {
                Id = RoleDefinitionRegistry.RoleIds.Administrator,
                Name = RoleConstants.Administrator,
                NormalizedName = RoleConstants.Administrator.ToUpperInvariant(),
                Description = "Administrative access with configurable permissions.",
                IsDefault = false,
                IsSystem = true,
                PriorityLevel = 10,
                CreatedOnUtc = utcNow,
            },
            new
            {
                Id = RoleDefinitionRegistry.RoleIds.Customer,
                Name = RoleConstants.Customer,
                NormalizedName = RoleConstants.Customer.ToUpperInvariant(),
                Description = "Default role for registered customers.",
                IsDefault = true,
                IsSystem = true,
                PriorityLevel = 1000,
                CreatedOnUtc = utcNow,
            });
    }
}
