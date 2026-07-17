using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Suppliers.Application.Errors;

public static class SupplierErrors
{
    public static readonly Error NotFound = new(
        "Supplier.NotFound",
        "The supplier was not found.");

    public static readonly Error CodeAlreadyExists = new(
        "Supplier.CodeAlreadyExists",
        "A supplier with this code already exists.");

    public static readonly Error ProviderTypeNotRegistered = new(
        "Supplier.ProviderTypeNotRegistered",
        "No supplier provider is registered for this provider type.");

    public static readonly Error MappingNotFound = new(
        "Supplier.MappingNotFound",
        "The supplier product mapping was not found.");
}
