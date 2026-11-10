namespace HAMBOX.Modules.Catalog.Application.Services;

/// <summary>
/// Permission names the Catalog module checks at field level (route-level checks live on the endpoints). The strings
/// must match <c>PermissionConstants.Catalog.*</c> in Identity, which a unit test asserts.
/// </summary>
public static class CatalogPermissions
{
    public const string ProductsView = "Catalog.Products.View";
    public const string ViewCosts = "Catalog.Inventory.ViewCosts";
    public const string RevealCodes = "Catalog.Inventory.RevealCodes";
}
