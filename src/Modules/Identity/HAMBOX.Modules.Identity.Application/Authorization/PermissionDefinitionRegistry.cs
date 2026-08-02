namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Single source of truth for all permission groups and permissions.
/// Add new modules here — seed data is generated from this registry.
/// </summary>
public static class PermissionDefinitionRegistry
{
    public sealed record GroupDefinition(
        Guid Id,
        string Key,
        string Name,
        string Module,
        int SortOrder,
        string? Description = null);

    public sealed record PermissionDefinition(
        Guid Id,
        Guid GroupId,
        string Name,
        int SortOrder,
        string? Description = null);

    public static class GroupIds
    {
        public static readonly Guid Dashboard = new("15000000-0000-0000-0000-000000000001");
        public static readonly Guid Catalog = new("15000000-0000-0000-0000-000000000002");
        public static readonly Guid Categories = new("15000000-0000-0000-0000-000000000003");
        public static readonly Guid Products = new("15000000-0000-0000-0000-000000000004");
        public static readonly Guid Inventory = new("15000000-0000-0000-0000-000000000005");
        public static readonly Guid Commerce = new("15000000-0000-0000-0000-000000000006");
        public static readonly Guid Orders = new("15000000-0000-0000-0000-000000000007");
        public static readonly Guid Customers = new("15000000-0000-0000-0000-000000000008");
        public static readonly Guid Users = new("15000000-0000-0000-0000-000000000009");
        public static readonly Guid Roles = new("15000000-0000-0000-0000-000000000010");
        public static readonly Guid Permissions = new("15000000-0000-0000-0000-000000000011");
        public static readonly Guid Memberships = new("15000000-0000-0000-0000-000000000012");
        public static readonly Guid Coupons = new("15000000-0000-0000-0000-000000000013");
        public static readonly Guid Themes = new("15000000-0000-0000-0000-000000000014");
        public static readonly Guid Reviews = new("15000000-0000-0000-0000-000000000015");
        public static readonly Guid Notifications = new("15000000-0000-0000-0000-000000000016");
        public static readonly Guid Referral = new("15000000-0000-0000-0000-000000000017");
        public static readonly Guid Reports = new("15000000-0000-0000-0000-000000000018");
        public static readonly Guid Localization = new("15000000-0000-0000-0000-000000000019");
        public static readonly Guid Settings = new("15000000-0000-0000-0000-000000000020");
        public static readonly Guid Support = new("15000000-0000-0000-0000-000000000021");
        public static readonly Guid Media = new("15000000-0000-0000-0000-000000000022");
        public static readonly Guid AuditLogs = new("15000000-0000-0000-0000-000000000023");
        public static readonly Guid Promotions = new("15000000-0000-0000-0000-000000000024");
        public static readonly Guid Operations = new("15000000-0000-0000-0000-000000000025");
        public static readonly Guid Analytics = new("15000000-0000-0000-0000-000000000026");
        public static readonly Guid Legal = new("15000000-0000-0000-0000-000000000027");
        public static readonly Guid Suppliers = new("15000000-0000-0000-0000-000000000028");
        public static readonly Guid Security = new("15000000-0000-0000-0000-000000000029");
        public static readonly Guid Communication = new("15000000-0000-0000-0000-000000000030");
        public static readonly Guid Collections = new("15000000-0000-0000-0000-000000000031");
        public static readonly Guid PageBuilder = new("15000000-0000-0000-0000-000000000032");
    }

    public static readonly IReadOnlyList<GroupDefinition> Groups =
    [
        new(GroupIds.Dashboard, "Dashboard", "Dashboard", "Dashboard", 10),
        new(GroupIds.Catalog, "Catalog", "Catalog", "Catalog", 20, "Catalog module overview"),
        new(GroupIds.Categories, "Categories", "Categories", "Catalog", 30),
        new(GroupIds.Products, "Products", "Products", "Catalog", 40),
        new(GroupIds.Inventory, "Inventory", "Inventory", "Catalog", 50),
        new(GroupIds.Commerce, "Commerce", "Commerce", "Commerce", 60),
        new(GroupIds.Orders, "Orders", "Orders", "Commerce", 70),
        new(GroupIds.Customers, "Customers", "Customers", "Commerce", 80),
        new(GroupIds.Users, "Users", "Users", "Identity", 90),
        new(GroupIds.Roles, "Roles", "Roles", "Identity", 100),
        new(GroupIds.Permissions, "Permissions", "Permissions", "Identity", 110),
        new(GroupIds.Memberships, "Memberships", "Memberships", "Commerce", 120),
        new(GroupIds.Coupons, "Coupons", "Coupons", "Commerce", 130),
        new(GroupIds.Themes, "Themes", "Themes", "Platform", 140),
        new(GroupIds.Reviews, "Reviews", "Reviews", "Commerce", 150),
        new(GroupIds.Notifications, "Notifications", "Notifications", "Commerce", 160),
        new(GroupIds.Referral, "Referral", "Referral", "Commerce", 170),
        new(GroupIds.Reports, "Reports", "Reports", "Analytics", 180),
        new(GroupIds.Localization, "Localization", "Localization", "Platform", 190),
        new(GroupIds.Settings, "Settings", "Settings", "Platform", 200),
        new(GroupIds.Support, "Support", "Support", "Support", 210),
        new(GroupIds.Media, "Media", "Media", "Catalog", 220),
        new(GroupIds.AuditLogs, "AuditLogs", "Audit Logs", "Security", 230),
        new(GroupIds.Promotions, "Promotions", "Promotions", "Commerce", 135),
        new(GroupIds.Operations, "Operations", "Operations", "Operations", 75),
        new(GroupIds.Analytics, "Analytics", "Analytics", "Analytics", 185),
        new(GroupIds.Legal, "Legal", "Legal Center", "Platform", 145),
        new(GroupIds.Suppliers, "Suppliers", "Suppliers", "Operations", 146),
        new(GroupIds.Security, "Security", "Security Center", "Security", 147, "Blocked users, emails, countries, IPs, and security events"),
        new(GroupIds.Communication, "Communication", "Communication Center", "Communication", 148, "Templates, notifications, providers, and delivery history"),
        new(GroupIds.Collections, "Collections", "Collections", "Catalog", 45, "Internal, owner-only product organization (never customer-facing)"),
        new(GroupIds.PageBuilder, "PageBuilder", "Page Builder", "Platform", 149, "Storefront homepage landing page templates and section arrangement"),
    ];

    public static readonly IReadOnlyList<PermissionDefinition> Permissions =
    [
        // Dashboard
        new(new Guid("20000000-0000-0000-0000-000000000001"), GroupIds.Dashboard, PermissionConstants.Dashboard.View, 1, "View admin dashboard"),

        // Categories
        new(new Guid("20000000-0000-0000-0000-000000000010"), GroupIds.Categories, PermissionConstants.Catalog.Categories.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000011"), GroupIds.Categories, PermissionConstants.Catalog.Categories.Create, 2),
        new(new Guid("20000000-0000-0000-0000-000000000012"), GroupIds.Categories, PermissionConstants.Catalog.Categories.Edit, 3),
        new(new Guid("20000000-0000-0000-0000-000000000013"), GroupIds.Categories, PermissionConstants.Catalog.Categories.Delete, 4),

        // Products
        new(new Guid("20000000-0000-0000-0000-000000000020"), GroupIds.Products, PermissionConstants.Catalog.Products.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000021"), GroupIds.Products, PermissionConstants.Catalog.Products.Create, 2),
        new(new Guid("20000000-0000-0000-0000-000000000022"), GroupIds.Products, PermissionConstants.Catalog.Products.Edit, 3),
        new(new Guid("20000000-0000-0000-0000-000000000023"), GroupIds.Products, PermissionConstants.Catalog.Products.Delete, 4),

        // Inventory
        new(new Guid("20000000-0000-0000-0000-000000000030"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000031"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.Edit, 2),
        new(new Guid("20000000-0000-0000-0000-000000000032"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.Create, 3),
        new(new Guid("20000000-0000-0000-0000-000000000033"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.Delete, 4),
        new(new Guid("20000000-0000-0000-0000-000000000034"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.Import, 5),
        new(new Guid("20000000-0000-0000-0000-000000000035"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.Export, 6),
        new(new Guid("20000000-0000-0000-0000-000000000036"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.ManageCodes, 7),
        new(new Guid("20000000-0000-0000-0000-000000000037"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.ViewCosts, 8),
        new(new Guid("20000000-0000-0000-0000-000000000038"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.ManageBatches, 9),
        new(new Guid("20000000-0000-0000-0000-000000000039"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.ManageSuppliers, 10),

        // Orders
        new(new Guid("20000000-0000-0000-0000-000000000040"), GroupIds.Orders, PermissionConstants.Orders.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000041"), GroupIds.Orders, PermissionConstants.Orders.Edit, 2),
        new(new Guid("20000000-0000-0000-0000-000000000042"), GroupIds.Orders, PermissionConstants.Orders.Refund, 3),

        // Customers
        new(new Guid("20000000-0000-0000-0000-000000000050"), GroupIds.Customers, PermissionConstants.Customers.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000051"), GroupIds.Customers, PermissionConstants.Customers.Edit, 2),

        // Users
        new(new Guid("20000000-0000-0000-0000-000000000060"), GroupIds.Users, PermissionConstants.Users.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000061"), GroupIds.Users, PermissionConstants.Users.Edit, 2),
        new(new Guid("20000000-0000-0000-0000-000000000062"), GroupIds.Users, PermissionConstants.Users.AssignRoles, 3),

        // Roles
        new(new Guid("20000000-0000-0000-0000-000000000070"), GroupIds.Roles, PermissionConstants.Roles.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000071"), GroupIds.Roles, PermissionConstants.Roles.Create, 2),
        new(new Guid("20000000-0000-0000-0000-000000000072"), GroupIds.Roles, PermissionConstants.Roles.Edit, 3),
        new(new Guid("20000000-0000-0000-0000-000000000073"), GroupIds.Roles, PermissionConstants.Roles.Delete, 4),
        new(new Guid("20000000-0000-0000-0000-000000000074"), GroupIds.Roles, PermissionConstants.Roles.AssignUsers, 5),

        // Permissions
        new(new Guid("20000000-0000-0000-0000-000000000080"), GroupIds.Permissions, PermissionConstants.Permissions.View, 1),

        // Memberships
        new(new Guid("20000000-0000-0000-0000-000000000090"), GroupIds.Memberships, PermissionConstants.Memberships.View, 1, "View membership plans and members"),
        new(new Guid("20000000-0000-0000-0000-000000000091"), GroupIds.Memberships, PermissionConstants.Memberships.Edit, 3, "Edit membership plans"),
        new(new Guid("20000000-0000-0000-0000-000000000092"), GroupIds.Memberships, PermissionConstants.Memberships.Create, 2, "Create membership plans"),
        new(new Guid("20000000-0000-0000-0000-000000000093"), GroupIds.Memberships, PermissionConstants.Memberships.Delete, 4, "Delete membership plans"),
        new(new Guid("20000000-0000-0000-0000-000000000094"), GroupIds.Memberships, PermissionConstants.Memberships.Assign, 5, "Assign memberships to users"),
        new(new Guid("20000000-0000-0000-0000-000000000095"), GroupIds.Memberships, PermissionConstants.Memberships.Renew, 6, "Renew member subscriptions"),
        new(new Guid("20000000-0000-0000-0000-000000000096"), GroupIds.Memberships, PermissionConstants.Memberships.Cancel, 7, "Cancel member subscriptions"),
        new(new Guid("20000000-0000-0000-0000-000000000097"), GroupIds.Memberships, PermissionConstants.Memberships.ConfigureBenefits, 8, "Configure plan benefits"),

        // Coupons
        new(new Guid("20000000-0000-0000-0000-000000000100"), GroupIds.Coupons, PermissionConstants.Coupons.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000101"), GroupIds.Coupons, PermissionConstants.Coupons.Create, 2),
        new(new Guid("20000000-0000-0000-0000-000000000102"), GroupIds.Coupons, PermissionConstants.Coupons.Edit, 3),
        new(new Guid("20000000-0000-0000-0000-000000000103"), GroupIds.Coupons, PermissionConstants.Coupons.Delete, 4),
        new(new Guid("20000000-0000-0000-0000-000000000104"), GroupIds.Coupons, PermissionConstants.Coupons.Generate, 5),
        new(new Guid("20000000-0000-0000-0000-000000000105"), GroupIds.Coupons, PermissionConstants.Coupons.Export, 6),
        new(new Guid("20000000-0000-0000-0000-000000000106"), GroupIds.Coupons, PermissionConstants.Coupons.Import, 7),

        // Promotions
        new(new Guid("20000000-0000-0000-0000-000000000210"), GroupIds.Promotions, PermissionConstants.Promotions.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000211"), GroupIds.Promotions, PermissionConstants.Promotions.Create, 2),
        new(new Guid("20000000-0000-0000-0000-000000000212"), GroupIds.Promotions, PermissionConstants.Promotions.Edit, 3),
        new(new Guid("20000000-0000-0000-0000-000000000213"), GroupIds.Promotions, PermissionConstants.Promotions.Delete, 4),
        new(new Guid("20000000-0000-0000-0000-000000000214"), GroupIds.Promotions, PermissionConstants.Promotions.Publish, 5),

        // Themes
        new(new Guid("20000000-0000-0000-0000-000000000110"), GroupIds.Themes, PermissionConstants.Themes.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000111"), GroupIds.Themes, PermissionConstants.Themes.Create, 2),
        new(new Guid("20000000-0000-0000-0000-000000000112"), GroupIds.Themes, PermissionConstants.Themes.Edit, 3),
        new(new Guid("20000000-0000-0000-0000-000000000113"), GroupIds.Themes, PermissionConstants.Themes.Delete, 4),
        new(new Guid("20000000-0000-0000-0000-000000000114"), GroupIds.Themes, PermissionConstants.Themes.Publish, 5),
        new(new Guid("20000000-0000-0000-0000-000000000115"), GroupIds.Themes, PermissionConstants.Themes.Schedule, 6),
        new(new Guid("20000000-0000-0000-0000-000000000116"), GroupIds.Themes, PermissionConstants.Themes.Assign, 7),
        new(new Guid("20000000-0000-0000-0000-000000000117"), GroupIds.Themes, PermissionConstants.Themes.Export, 8),
        new(new Guid("20000000-0000-0000-0000-000000000118"), GroupIds.Themes, PermissionConstants.Themes.Import, 9),
        new(new Guid("20000000-0000-0000-0000-000000000119"), GroupIds.Themes, PermissionConstants.Themes.Rollback, 10),

        // Reviews
        new(new Guid("20000000-0000-0000-0000-000000000120"), GroupIds.Reviews, PermissionConstants.Reviews.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000121"), GroupIds.Reviews, PermissionConstants.Reviews.Moderate, 2),

        // Notifications
        new(new Guid("20000000-0000-0000-0000-000000000130"), GroupIds.Notifications, PermissionConstants.Notifications.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000131"), GroupIds.Notifications, PermissionConstants.Notifications.Send, 2),

        // Referral
        new(new Guid("20000000-0000-0000-0000-000000000140"), GroupIds.Referral, PermissionConstants.Referral.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000141"), GroupIds.Referral, PermissionConstants.Referral.Manage, 2),

        // Reports
        new(new Guid("20000000-0000-0000-0000-000000000150"), GroupIds.Reports, PermissionConstants.Reports.View, 1),

        // Localization
        new(new Guid("20000000-0000-0000-0000-000000000160"), GroupIds.Localization, PermissionConstants.Localization.Manage, 1),

        // Settings
        new(new Guid("20000000-0000-0000-0000-000000000170"), GroupIds.Settings, PermissionConstants.Settings.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000171"), GroupIds.Settings, PermissionConstants.Settings.Edit, 2),

        // Support
        new(new Guid("20000000-0000-0000-0000-000000000180"), GroupIds.Support, PermissionConstants.Support.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000181"), GroupIds.Support, PermissionConstants.Support.Manage, 2),

        // Media
        new(new Guid("20000000-0000-0000-0000-000000000190"), GroupIds.Media, PermissionConstants.Media.Upload, 1),
        new(new Guid("20000000-0000-0000-0000-000000000191"), GroupIds.Media, PermissionConstants.Media.Delete, 2),

        // Audit Logs
        new(new Guid("20000000-0000-0000-0000-000000000200"), GroupIds.AuditLogs, PermissionConstants.AuditLogs.View, 1),

        // Operations (220–224; 210–214 are Promotions)
        new(new Guid("20000000-0000-0000-0000-000000000220"), GroupIds.Operations, PermissionConstants.Operations.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000221"), GroupIds.Operations, PermissionConstants.Operations.Manage, 2),
        new(new Guid("20000000-0000-0000-0000-000000000222"), GroupIds.Operations, PermissionConstants.Operations.Retry, 3),
        new(new Guid("20000000-0000-0000-0000-000000000223"), GroupIds.Operations, PermissionConstants.Operations.Export, 4),
        new(new Guid("20000000-0000-0000-0000-000000000224"), GroupIds.Operations, PermissionConstants.Operations.Clear, 5),

        // Analytics (225–228)
        new(new Guid("20000000-0000-0000-0000-000000000225"), GroupIds.Analytics, PermissionConstants.Analytics.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000226"), GroupIds.Analytics, PermissionConstants.Analytics.Export, 2),
        new(new Guid("20000000-0000-0000-0000-000000000227"), GroupIds.Analytics, PermissionConstants.Analytics.Compare, 3),
        new(new Guid("20000000-0000-0000-0000-000000000228"), GroupIds.Analytics, PermissionConstants.Analytics.Manage, 4),

        // Reports Export/Schedule/Delete (229-231) appended to preserve RolePermission seed indices
        new(new Guid("20000000-0000-0000-0000-000000000229"), GroupIds.Reports, PermissionConstants.Reports.Export, 2),
        new(new Guid("20000000-0000-0000-0000-000000000230"), GroupIds.Reports, PermissionConstants.Reports.Schedule, 3),
        new(new Guid("20000000-0000-0000-0000-000000000231"), GroupIds.Reports, PermissionConstants.Reports.Delete, 4),

        // Inventory.RevealCodes appended to preserve RolePermission seed indices
        new(new Guid("21000000-0000-0000-0000-000000000001"), GroupIds.Inventory, PermissionConstants.Catalog.Inventory.RevealCodes, 11),

        // Legal Center (232-235) appended to preserve RolePermission seed indices
        new(new Guid("20000000-0000-0000-0000-000000000232"), GroupIds.Legal, PermissionConstants.Legal.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000233"), GroupIds.Legal, PermissionConstants.Legal.Create, 2),
        new(new Guid("20000000-0000-0000-0000-000000000234"), GroupIds.Legal, PermissionConstants.Legal.Edit, 3),
        new(new Guid("20000000-0000-0000-0000-000000000235"), GroupIds.Legal, PermissionConstants.Legal.Publish, 4),

        // Suppliers (236-240) appended to preserve RolePermission seed indices
        new(new Guid("20000000-0000-0000-0000-000000000236"), GroupIds.Suppliers, PermissionConstants.Suppliers.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000237"), GroupIds.Suppliers, PermissionConstants.Suppliers.Create, 2),
        new(new Guid("20000000-0000-0000-0000-000000000238"), GroupIds.Suppliers, PermissionConstants.Suppliers.Edit, 3),
        new(new Guid("20000000-0000-0000-0000-000000000239"), GroupIds.Suppliers, PermissionConstants.Suppliers.Delete, 4),
        new(new Guid("20000000-0000-0000-0000-000000000240"), GroupIds.Suppliers, PermissionConstants.Suppliers.ManageMappings, 5),

        // Security Center (241-246) appended to preserve RolePermission seed indices
        new(new Guid("20000000-0000-0000-0000-000000000241"), GroupIds.Security, PermissionConstants.Security.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000242"), GroupIds.Security, PermissionConstants.Security.ManageUsers, 2),
        new(new Guid("20000000-0000-0000-0000-000000000243"), GroupIds.Security, PermissionConstants.Security.ManageEmails, 3),
        new(new Guid("20000000-0000-0000-0000-000000000244"), GroupIds.Security, PermissionConstants.Security.ManageCountries, 4),
        new(new Guid("20000000-0000-0000-0000-000000000245"), GroupIds.Security, PermissionConstants.Security.ManageIPs, 5),
        new(new Guid("20000000-0000-0000-0000-000000000246"), GroupIds.Security, PermissionConstants.Security.ViewEvents, 6),

        // Communication Center (247-251) appended to preserve RolePermission seed indices
        new(new Guid("20000000-0000-0000-0000-000000000247"), GroupIds.Communication, PermissionConstants.Communication.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000248"), GroupIds.Communication, PermissionConstants.Communication.Manage, 2),
        new(new Guid("20000000-0000-0000-0000-000000000249"), GroupIds.Communication, PermissionConstants.Communication.Send, 3),
        new(new Guid("20000000-0000-0000-0000-000000000250"), GroupIds.Communication, PermissionConstants.Communication.ManageTemplates, 4),
        new(new Guid("20000000-0000-0000-0000-000000000251"), GroupIds.Communication, PermissionConstants.Communication.ManageProviders, 5),

        new(new Guid("20000000-0000-0000-0000-000000000252"), GroupIds.Legal, PermissionConstants.Legal.Delete, 5),

        // Collections (253-256) appended to preserve RolePermission seed indices
        new(new Guid("20000000-0000-0000-0000-000000000253"), GroupIds.Collections, PermissionConstants.Catalog.Collections.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000254"), GroupIds.Collections, PermissionConstants.Catalog.Collections.Create, 2),
        new(new Guid("20000000-0000-0000-0000-000000000255"), GroupIds.Collections, PermissionConstants.Catalog.Collections.Edit, 3),
        new(new Guid("20000000-0000-0000-0000-000000000256"), GroupIds.Collections, PermissionConstants.Catalog.Collections.Delete, 4),

        // Page Builder (257-259) appended to preserve RolePermission seed indices
        new(new Guid("20000000-0000-0000-0000-000000000257"), GroupIds.PageBuilder, PermissionConstants.PageBuilder.View, 1),
        new(new Guid("20000000-0000-0000-0000-000000000258"), GroupIds.PageBuilder, PermissionConstants.PageBuilder.Edit, 2),
        new(new Guid("20000000-0000-0000-0000-000000000259"), GroupIds.PageBuilder, PermissionConstants.PageBuilder.Publish, 3),

        // Support module granular permissions (260-268) appended to preserve RolePermission seed indices
        new(new Guid("20000000-0000-0000-0000-000000000260"), GroupIds.Support, PermissionConstants.Support.Create, 3),
        new(new Guid("20000000-0000-0000-0000-000000000261"), GroupIds.Support, PermissionConstants.Support.Reply, 4),
        new(new Guid("20000000-0000-0000-0000-000000000262"), GroupIds.Support, PermissionConstants.Support.Assign, 5),
        new(new Guid("20000000-0000-0000-0000-000000000263"), GroupIds.Support, PermissionConstants.Support.Close, 6),
        new(new Guid("20000000-0000-0000-0000-000000000264"), GroupIds.Support, PermissionConstants.Support.Delete, 7),
        new(new Guid("20000000-0000-0000-0000-000000000265"), GroupIds.Support, PermissionConstants.Support.ManageCategories, 8),
        new(new Guid("20000000-0000-0000-0000-000000000266"), GroupIds.Support, PermissionConstants.Support.ManageSavedReplies, 9),
        new(new Guid("20000000-0000-0000-0000-000000000267"), GroupIds.Support, PermissionConstants.Support.ManageKnowledgeBase, 10),
        new(new Guid("20000000-0000-0000-0000-000000000268"), GroupIds.Support, PermissionConstants.Support.ViewAnalytics, 11),

        // Security Center redesign (269-270) appended to preserve RolePermission seed indices
        new(new Guid("20000000-0000-0000-0000-000000000269"), GroupIds.Security, PermissionConstants.Security.ManageAlerts, 7),
        new(new Guid("20000000-0000-0000-0000-000000000270"), GroupIds.Security, PermissionConstants.Security.ManageDevices, 8),
    ];

    public static IReadOnlyCollection<string> AllPermissionNames =>
        Permissions.Select(p => p.Name).ToArray();

    public static Guid? GetPermissionId(string name) =>
        Permissions.FirstOrDefault(p => p.Name == name)?.Id;

    public static IReadOnlyCollection<Guid> AllPermissionIds =>
        Permissions.Select(p => p.Id).ToArray();
}
