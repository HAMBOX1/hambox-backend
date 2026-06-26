namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Contains all well-known permissions defined in the system.
/// </summary>
public static class PermissionConstants
{
    /// <summary>
    /// Permissions related to product management.
    /// </summary>
    public static class Products
    {
        /// <summary>
        /// Permission to create products.
        /// </summary>
        public const string Create = "Products.Create";

        /// <summary>
        /// Permission to update products.
        /// </summary>
        public const string Update = "Products.Update";

        /// <summary>
        /// Permission to delete products.
        /// </summary>
        public const string Delete = "Products.Delete";
    }

    /// <summary>
    /// Permissions related to category management.
    /// </summary>
    public static class Categories
    {
        /// <summary>
        /// Permission to create categories.
        /// </summary>
        public const string Create = "Categories.Create";

        /// <summary>
        /// Permission to update categories.
        /// </summary>
        public const string Update = "Categories.Update";

        /// <summary>
        /// Permission to delete categories.
        /// </summary>
        public const string Delete = "Categories.Delete";
    }

    /// <summary>
    /// Permissions related to user account management.
    /// </summary>
    public static class Users
    {
        /// <summary>
        /// Permission to view users.
        /// </summary>
        public const string Read = "Users.Read";

        /// <summary>
        /// Permission to edit users.
        /// </summary>
        public const string Update = "Users.Update";
    }

    /// <summary>
    /// Permissions related to system role management.
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// Permission to manage roles and role assignments.
        /// </summary>
        public const string Manage = "Roles.Manage";
    }

    /// <summary>
    /// Collection of all defined permissions.
    /// </summary>
    public static readonly IReadOnlyCollection<string> All =
    [
        Products.Create,
        Products.Update,
        Products.Delete,
        Categories.Create,
        Categories.Update,
        Categories.Delete,
        Users.Read,
        Users.Update,
        Roles.Manage
    ];
}
