namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Strongly typed permission names. Values match database seed from <see cref="PermissionDefinitionRegistry"/>.
/// </summary>
public static class PermissionConstants
{
    public static class Dashboard
    {
        public const string View = "Dashboard.View";
    }

    public static class Catalog
    {
        public static class Categories
        {
            public const string View = "Catalog.Categories.View";
            public const string Create = "Catalog.Categories.Create";
            public const string Edit = "Catalog.Categories.Edit";
            public const string Delete = "Catalog.Categories.Delete";
        }

        public static class Products
        {
            public const string View = "Catalog.Products.View";
            public const string Create = "Catalog.Products.Create";
            public const string Edit = "Catalog.Products.Edit";
            public const string Delete = "Catalog.Products.Delete";
        }

        public static class Inventory
        {
            public const string View = "Catalog.Inventory.View";
            public const string Create = "Catalog.Inventory.Create";
            public const string Edit = "Catalog.Inventory.Edit";
            public const string Delete = "Catalog.Inventory.Delete";
            public const string Import = "Catalog.Inventory.Import";
            public const string Export = "Catalog.Inventory.Export";
            public const string ManageCodes = "Catalog.Inventory.ManageCodes";
            public const string ViewCosts = "Catalog.Inventory.ViewCosts";
            public const string ManageBatches = "Catalog.Inventory.ManageBatches";
            public const string ManageSuppliers = "Catalog.Inventory.ManageSuppliers";
        }
    }

    public static class Orders
    {
        public const string View = "Orders.View";
        public const string Edit = "Orders.Edit";
        public const string Refund = "Orders.Refund";
    }

    public static class Customers
    {
        public const string View = "Customers.View";
        public const string Edit = "Customers.Edit";
    }

    public static class Users
    {
        public const string View = "Users.View";
        public const string Edit = "Users.Edit";
        public const string AssignRoles = "Users.AssignRoles";
    }

    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Create = "Roles.Create";
        public const string Edit = "Roles.Edit";
        public const string Delete = "Roles.Delete";
        public const string AssignUsers = "Roles.AssignUsers";
    }

    public static class Permissions
    {
        public const string View = "Permissions.View";
    }

    public static class Memberships
    {
        public const string View = "Memberships.View";
        public const string Create = "Memberships.Create";
        public const string Edit = "Memberships.Edit";
        public const string Delete = "Memberships.Delete";
        public const string Assign = "Memberships.Assign";
        public const string Renew = "Memberships.Renew";
        public const string Cancel = "Memberships.Cancel";
        public const string ConfigureBenefits = "Memberships.ConfigureBenefits";
    }

    public static class Coupons
    {
        public const string View = "Coupons.View";
        public const string Create = "Coupons.Create";
        public const string Edit = "Coupons.Edit";
        public const string Delete = "Coupons.Delete";
        public const string Generate = "Coupons.Generate";
        public const string Export = "Coupons.Export";
        public const string Import = "Coupons.Import";
    }

    public static class Promotions
    {
        public const string View = "Promotions.View";
        public const string Create = "Promotions.Create";
        public const string Edit = "Promotions.Edit";
        public const string Delete = "Promotions.Delete";
        public const string Publish = "Promotions.Publish";
    }

    public static class Themes
    {
        public const string View = "Themes.View";
        public const string Create = "Themes.Create";
        public const string Edit = "Themes.Edit";
        public const string Delete = "Themes.Delete";
        public const string Publish = "Themes.Publish";
        public const string Schedule = "Themes.Schedule";
        public const string Assign = "Themes.Assign";
        public const string Export = "Themes.Export";
        public const string Import = "Themes.Import";
        public const string Rollback = "Themes.Rollback";

        [Obsolete("Use granular Themes.* permissions.")]
        public const string Manage = "Themes.Manage";
    }

    public static class Reviews
    {
        public const string View = "Reviews.View";
        public const string Moderate = "Reviews.Moderate";
    }

    public static class Notifications
    {
        public const string View = "Notifications.View";
        public const string Send = "Notifications.Send";
    }

    public static class Referral
    {
        public const string View = "Referral.View";
        public const string Manage = "Referral.Manage";
    }

    public static class Reports
    {
        public const string View = "Reports.View";
        public const string Export = "Reports.Export";
        public const string Schedule = "Reports.Schedule";
        public const string Delete = "Reports.Delete";
    }

    public static class Localization
    {
        public const string Manage = "Localization.Manage";
    }

    public static class Settings
    {
        public const string View = "Settings.View";
        public const string Edit = "Settings.Edit";
    }

    public static class Support
    {
        public const string View = "Support.View";
        public const string Manage = "Support.Manage";
    }

    public static class Media
    {
        public const string Upload = "Media.Upload";
        public const string Delete = "Media.Delete";
    }

    public static class AuditLogs
    {
        public const string View = "AuditLogs.View";
    }

    public static class Operations
    {
        public const string View = "Operations.View";
        public const string Manage = "Operations.Manage";
        public const string Retry = "Operations.Retry";
        public const string Export = "Operations.Export";
        public const string Clear = "Operations.Clear";
    }

    public static class Analytics
    {
        public const string View = "Analytics.View";
        public const string Export = "Analytics.Export";
        public const string Compare = "Analytics.Compare";
        public const string Manage = "Analytics.Manage";
    }

    /// <summary>All permission names for policy registration.</summary>
    public static readonly IReadOnlyCollection<string> All =
    [
        Dashboard.View,
        Catalog.Categories.View, Catalog.Categories.Create, Catalog.Categories.Edit, Catalog.Categories.Delete,
        Catalog.Products.View, Catalog.Products.Create, Catalog.Products.Edit, Catalog.Products.Delete,
        Catalog.Inventory.View, Catalog.Inventory.Create, Catalog.Inventory.Edit, Catalog.Inventory.Delete,
        Catalog.Inventory.Import, Catalog.Inventory.Export, Catalog.Inventory.ManageCodes,
        Catalog.Inventory.ViewCosts, Catalog.Inventory.ManageBatches, Catalog.Inventory.ManageSuppliers,
        Orders.View, Orders.Edit, Orders.Refund,
        Customers.View, Customers.Edit,
        Users.View, Users.Edit, Users.AssignRoles,
        Roles.View, Roles.Create, Roles.Edit, Roles.Delete, Roles.AssignUsers,
        Permissions.View,
        Memberships.View, Memberships.Create, Memberships.Edit, Memberships.Delete,
        Memberships.Assign, Memberships.Renew, Memberships.Cancel, Memberships.ConfigureBenefits,
        Coupons.View, Coupons.Create, Coupons.Edit, Coupons.Delete, Coupons.Generate, Coupons.Export, Coupons.Import,
        Promotions.View, Promotions.Create, Promotions.Edit, Promotions.Delete, Promotions.Publish,
        Themes.View, Themes.Create, Themes.Edit, Themes.Delete, Themes.Publish,
        Themes.Schedule, Themes.Assign, Themes.Export, Themes.Import, Themes.Rollback,
        Reviews.View, Reviews.Moderate,
        Notifications.View, Notifications.Send,
        Referral.View, Referral.Manage,
        Reports.View, Reports.Export, Reports.Schedule, Reports.Delete,
        Localization.Manage,
        Settings.View, Settings.Edit,
        Support.View, Support.Manage,
        Media.Upload, Media.Delete,
        AuditLogs.View,
        Operations.View, Operations.Manage, Operations.Retry, Operations.Export, Operations.Clear,
        Analytics.View, Analytics.Export, Analytics.Compare, Analytics.Manage,
    ];
}
