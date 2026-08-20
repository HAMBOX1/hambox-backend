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

    public static readonly Error MappingAlreadyExists = new(
        "Supplier.MappingAlreadyExists",
        "This product is already mapped to this supplier.");

    public static readonly Error SupplierDisabled = new(
        "Supplier.SupplierDisabled",
        "This supplier is disabled and cannot be used for new mappings. Enable it first.");

    public static readonly Error InvalidFulfillmentQuantity = new(
        "Supplier.InvalidFulfillmentQuantity",
        "Requested fulfillment quantity must be greater than zero.");

    public static readonly Error MappingBelongsToAnotherSupplier = new(
        "Supplier.MappingBelongsToAnotherSupplier",
        "This product mapping does not belong to the specified supplier.");

    public static readonly Error MappingInactive = new(
        "Supplier.MappingInactive",
        "This supplier product mapping is not active.");

    public static readonly Error FulfillmentNotFound = new(
        "Supplier.FulfillmentNotFound",
        "The supplier fulfillment attempt was not found.");

    public static readonly Error ConcurrentClaimLost = new(
        "Supplier.ConcurrentClaimLost",
        "Another worker already claimed this fulfillment attempt.");
}
