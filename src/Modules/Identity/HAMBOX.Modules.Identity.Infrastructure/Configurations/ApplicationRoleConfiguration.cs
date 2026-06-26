using HAMBOX.Modules.Identity.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="ApplicationRole"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    /// <summary>
    /// Well-known role identifiers for seed data.
    /// </summary>
    internal static class SeedIds
    {
        internal static readonly Guid SuperAdmin = new("10000000-0000-0000-0000-000000000001");
        internal static readonly Guid Admin = new("10000000-0000-0000-0000-000000000002");
        internal static readonly Guid ContentManager = new("10000000-0000-0000-0000-000000000003");
        internal static readonly Guid SupportAgent = new("10000000-0000-0000-0000-000000000004");
        internal static readonly Guid Customer = new("10000000-0000-0000-0000-000000000005");
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles");

        // Primary key
        builder.HasKey(r => r.Id);

        // Properties
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.NormalizedName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.Property(r => r.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        // Base entity properties
        builder.Property(r => r.CreatedOnUtc)
            .IsRequired();

        builder.Property(r => r.ModifiedOnUtc);

        // Concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // PermissionIds stored as a primitive collection (EF Core 8+)
        builder.PrimitiveCollection(r => r.PermissionIds)
            .HasColumnName("PermissionIds")
            .HasColumnType("nvarchar(max)");

        // Indexes
        builder.HasIndex(r => r.NormalizedName)
            .IsUnique()
            .HasDatabaseName("IX_Roles_NormalizedName");

        // Ignore domain events collection
        builder.Ignore(r => r.DomainEvents);

        // Seed data
        SeedRoles(builder);
    }

    private static void SeedRoles(EntityTypeBuilder<ApplicationRole> builder)
    {
        var utcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Define permissions assigned to each role
        var superAdminPermissions = new List<Guid>
        {
            PermissionConfiguration.SeedIds.ProductsCreate,
            PermissionConfiguration.SeedIds.ProductsUpdate,
            PermissionConfiguration.SeedIds.ProductsDelete,
            PermissionConfiguration.SeedIds.CategoriesCreate,
            PermissionConfiguration.SeedIds.CategoriesUpdate,
            PermissionConfiguration.SeedIds.CategoriesDelete,
            PermissionConfiguration.SeedIds.UsersRead,
            PermissionConfiguration.SeedIds.UsersUpdate,
            PermissionConfiguration.SeedIds.RolesManage
        };

        var adminPermissions = new List<Guid>
        {
            PermissionConfiguration.SeedIds.ProductsCreate,
            PermissionConfiguration.SeedIds.ProductsUpdate,
            PermissionConfiguration.SeedIds.ProductsDelete,
            PermissionConfiguration.SeedIds.CategoriesCreate,
            PermissionConfiguration.SeedIds.CategoriesUpdate,
            PermissionConfiguration.SeedIds.CategoriesDelete,
            PermissionConfiguration.SeedIds.UsersRead,
            PermissionConfiguration.SeedIds.UsersUpdate
        };

        var contentManagerPermissions = new List<Guid>
        {
            PermissionConfiguration.SeedIds.ProductsCreate,
            PermissionConfiguration.SeedIds.ProductsUpdate,
            PermissionConfiguration.SeedIds.CategoriesCreate,
            PermissionConfiguration.SeedIds.CategoriesUpdate
        };

        var supportAgentPermissions = new List<Guid>
        {
            PermissionConfiguration.SeedIds.UsersRead
        };

        var customerPermissions = new List<Guid>();

        builder.HasData(
            CreateRole(SeedIds.SuperAdmin, "SuperAdmin", "Full system access with all privileges.", false, superAdminPermissions, utcNow),
            CreateRole(SeedIds.Admin, "Admin", "Administrative access for managing users and content.", false, adminPermissions, utcNow),
            CreateRole(SeedIds.ContentManager, "ContentManager", "Manages content creation and publishing.", false, contentManagerPermissions, utcNow),
            CreateRole(SeedIds.SupportAgent, "SupportAgent", "Handles customer support requests.", false, supportAgentPermissions, utcNow),
            CreateRole(SeedIds.Customer, "Customer", "Default role for registered customers.", true, customerPermissions, utcNow));
    }

    private static object CreateRole(
        Guid id,
        string name,
        string description,
        bool isDefault,
        List<Guid> permissionIds,
        DateTimeOffset createdOnUtc)
    {
        return new
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Description = description,
            IsDefault = isDefault,
            PermissionIds = permissionIds,
            CreatedOnUtc = createdOnUtc,
        };
    }
}
