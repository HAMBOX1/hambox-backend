using System.Reflection;

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
            public const string Import = "Catalog.Products.Import";
            public const string Export = "Catalog.Products.Export";
        }

        public static class Collections
        {
            public const string View = "Catalog.Collections.View";
            public const string Create = "Catalog.Collections.Create";
            public const string Edit = "Catalog.Collections.Edit";
            public const string Delete = "Catalog.Collections.Delete";
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
            public const string RevealCodes = "Catalog.Inventory.RevealCodes";
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

    public static class Legal
    {
        public const string View = "Legal.View";
        public const string Create = "Legal.Create";
        public const string Edit = "Legal.Edit";
        public const string Publish = "Legal.Publish";
        public const string Delete = "Legal.Delete";
    }

    public static class Suppliers
    {
        public const string View = "Suppliers.View";
        public const string Create = "Suppliers.Create";
        public const string Edit = "Suppliers.Edit";
        public const string Delete = "Suppliers.Delete";
        public const string ManageMappings = "Suppliers.ManageMappings";
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
        public const string Create = "Support.Create";
        public const string Reply = "Support.Reply";
        public const string Assign = "Support.Assign";
        public const string Close = "Support.Close";
        public const string Delete = "Support.Delete";
        public const string ManageCategories = "Support.ManageCategories";
        public const string ManageSavedReplies = "Support.ManageSavedReplies";
        public const string ManageKnowledgeBase = "Support.ManageKnowledgeBase";
        public const string ViewAnalytics = "Support.ViewAnalytics";
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

    public static class Security
    {
        public const string View = "Security.View";
        public const string ManageUsers = "Security.ManageUsers";
        public const string ManageEmails = "Security.ManageEmails";
        public const string ManageCountries = "Security.ManageCountries";
        public const string ManageIPs = "Security.ManageIPs";
        public const string ViewEvents = "Security.ViewEvents";
    }

    public static class Communication
    {
        public const string View = "Communication.View";
        public const string Manage = "Communication.Manage";
        public const string Send = "Communication.Send";
        public const string ManageTemplates = "Communication.ManageTemplates";
        public const string ManageProviders = "Communication.ManageProviders";
    }

    public static class PageBuilder
    {
        public const string View = "PageBuilder.View";
        public const string Edit = "PageBuilder.Edit";
        public const string Publish = "PageBuilder.Publish";
    }

    /// <summary>
    /// Every non-obsolete permission name declared anywhere in this class, discovered by reflection
    /// so a new <c>public const string</c> added under any nested group is automatically included —
    /// no hand-maintained list to fall out of sync. Drives authorization-policy registration
    /// (<c>IdentityInfrastructureExtensions.AddAuthorization</c>) and the Owner role's resolved
    /// permission set (<c>PermissionResolver</c>); also the source enumerated by
    /// <c>PermissionSynchronizer</c> to keep the Owner role's granted permissions current.
    /// </summary>
    public static readonly IReadOnlyCollection<string> All = DiscoverAll();

    private static IReadOnlyCollection<string> DiscoverAll()
    {
        var names = new List<string>();
        CollectFrom(typeof(PermissionConstants), names);
        return names.AsReadOnly();
    }

    private static void CollectFrom(Type type, List<string> names)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(flags))
        {
            if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
            if (field.GetCustomAttribute<ObsoleteAttribute>() is not null) continue;
            if (field.GetRawConstantValue() is string value) names.Add(value);
        }

        foreach (var nested in type.GetNestedTypes(flags))
        {
            CollectFrom(nested, names);
        }
    }
}
