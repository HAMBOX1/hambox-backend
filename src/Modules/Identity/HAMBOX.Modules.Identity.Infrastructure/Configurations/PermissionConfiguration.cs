using HAMBOX.Modules.Identity.Domain.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="Permission"/> entity for Entity Framework Core.
/// </summary>
internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <summary>
    /// Well-known permission identifiers for seed data.
    /// </summary>
    internal static class SeedIds
    {
        internal static readonly Guid ProductsCreate = new("20000000-0000-0000-0000-000000000001");
        internal static readonly Guid ProductsUpdate = new("20000000-0000-0000-0000-000000000002");
        internal static readonly Guid ProductsDelete = new("20000000-0000-0000-0000-000000000003");

        internal static readonly Guid CategoriesCreate = new("20000000-0000-0000-0000-000000000004");
        internal static readonly Guid CategoriesUpdate = new("20000000-0000-0000-0000-000000000005");
        internal static readonly Guid CategoriesDelete = new("20000000-0000-0000-0000-000000000006");

        internal static readonly Guid UsersRead = new("20000000-0000-0000-0000-000000000007");
        internal static readonly Guid UsersUpdate = new("20000000-0000-0000-0000-000000000008");

        internal static readonly Guid RolesManage = new("20000000-0000-0000-0000-000000000009");
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        // Primary key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.NormalizedName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        // Base entity properties
        builder.Property(p => p.CreatedOnUtc)
            .IsRequired();

        builder.Property(p => p.ModifiedOnUtc);

        // Indexes
        builder.HasIndex(p => p.NormalizedName)
            .IsUnique()
            .HasDatabaseName("IX_Permissions_NormalizedName");

        // Seed data
        SeedPermissions(builder);
    }

    private static void SeedPermissions(EntityTypeBuilder<Permission> builder)
    {
        var utcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            CreatePermission(SeedIds.ProductsCreate, "Products.Create", "Allows creating products.", utcNow),
            CreatePermission(SeedIds.ProductsUpdate, "Products.Update", "Allows updating products.", utcNow),
            CreatePermission(SeedIds.ProductsDelete, "Products.Delete", "Allows deleting products.", utcNow),
            CreatePermission(SeedIds.CategoriesCreate, "Categories.Create", "Allows creating categories.", utcNow),
            CreatePermission(SeedIds.CategoriesUpdate, "Categories.Update", "Allows updating categories.", utcNow),
            CreatePermission(SeedIds.CategoriesDelete, "Categories.Delete", "Allows deleting categories.", utcNow),
            CreatePermission(SeedIds.UsersRead, "Users.Read", "Allows reading user accounts.", utcNow),
            CreatePermission(SeedIds.UsersUpdate, "Users.Update", "Allows updating user accounts.", utcNow),
            CreatePermission(SeedIds.RolesManage, "Roles.Manage", "Allows managing system roles.", utcNow));
    }

    private static object CreatePermission(Guid id, string name, string description, DateTimeOffset createdOnUtc)
    {
        return new
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Description = description,
            CreatedOnUtc = createdOnUtc
        };
    }
}
