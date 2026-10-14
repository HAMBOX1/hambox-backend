namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// The staff roles the scope lists as "preferred" beyond Owner / Administrator / Customer. They are seeded once
/// (as system roles, so they cannot be deleted) with a sensible starting permission set; an owner can still
/// change what each one grants from the Roles page, and the synchronizer never overwrites an existing role.
/// None of them is granted the plaintext-reveal permissions (<see cref="PermissionConstants.Orders.RevealLicenseKeys"/>,
/// <see cref="PermissionConstants.Catalog.Inventory.RevealCodes"/>) — revealing a code stays an explicit grant.
/// </summary>
public static class PresetRoleDefinitions
{
    public sealed record PresetRole(string Name, string Description, IReadOnlyList<string> Permissions);

    public static IReadOnlyList<PresetRole> All { get; } =
    [
        new PresetRole(
            "Orders Manager",
            "Processes orders and customers: view/edit orders, handle refunds, reply to customers.",
            [
                PermissionConstants.Dashboard.View,
                PermissionConstants.Orders.View,
                PermissionConstants.Orders.Edit,
                PermissionConstants.Orders.Refund,
                PermissionConstants.Customers.View,
                PermissionConstants.Customers.Edit,
                PermissionConstants.Support.View,
                PermissionConstants.Support.Reply,
                PermissionConstants.Notifications.View,
            ]),
        new PresetRole(
            "Products Manager",
            "Maintains the catalog: products, categories, collections, stock and codes (no code reveal, no costs).",
            [
                PermissionConstants.Dashboard.View,
                PermissionConstants.Catalog.Categories.View,
                PermissionConstants.Catalog.Categories.Create,
                PermissionConstants.Catalog.Categories.Edit,
                PermissionConstants.Catalog.Products.View,
                PermissionConstants.Catalog.Products.Create,
                PermissionConstants.Catalog.Products.Edit,
                PermissionConstants.Catalog.Products.Import,
                PermissionConstants.Catalog.Products.Export,
                PermissionConstants.Catalog.Collections.View,
                PermissionConstants.Catalog.Collections.Create,
                PermissionConstants.Catalog.Collections.Edit,
                PermissionConstants.Catalog.Inventory.View,
                PermissionConstants.Catalog.Inventory.Create,
                PermissionConstants.Catalog.Inventory.Edit,
                PermissionConstants.Catalog.Inventory.Import,
                PermissionConstants.Catalog.Inventory.ManageCodes,
                PermissionConstants.Catalog.Inventory.ManageBatches,
                PermissionConstants.Suppliers.View,
            ]),
        new PresetRole(
            "Support Agent",
            "Answers customers: support tickets and saved replies, read-only access to orders and customers.",
            [
                PermissionConstants.Dashboard.View,
                PermissionConstants.Support.View,
                PermissionConstants.Support.Create,
                PermissionConstants.Support.Reply,
                PermissionConstants.Support.Assign,
                PermissionConstants.Support.Close,
                PermissionConstants.Support.ManageSavedReplies,
                PermissionConstants.Orders.View,
                PermissionConstants.Customers.View,
                PermissionConstants.Faq.View,
                PermissionConstants.Notifications.View,
            ]),
        new PresetRole(
            "Read-only Auditor",
            "Can look at everything that is a plain 'view', changes nothing and reveals no secrets.",
            ReadOnlyPermissions()),
    ];

    /// <summary>Every `*.View` permission plus the explicit read-only "view" style ones.</summary>
    private static IReadOnlyList<string> ReadOnlyPermissions() =>
        PermissionConstants.All
            .Where(name => name.EndsWith(".View", StringComparison.Ordinal)
                || name is PermissionConstants.Security.ViewEvents
                    or PermissionConstants.Support.ViewAnalytics
                    or PermissionConstants.Catalog.Inventory.ViewCosts)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
