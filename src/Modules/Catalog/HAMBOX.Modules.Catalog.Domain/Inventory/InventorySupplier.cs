using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

/// <summary>
/// A vendor-contact record used to tag manually-imported inventory (<see cref="InventoryBatch"/>,
/// <see cref="DigitalInventoryCode"/>) with "who we bought this from" — nothing more. It has no
/// credentials, provider type, or API integration concept.
/// </summary>
/// <remarks>
/// This is a different, older entity than <c>HAMBOX.Modules.Suppliers.Domain.Suppliers.Supplier</c>
/// (schema <c>suppliers</c>), which is the canonical registry for any future <em>automated</em>
/// supplier integration. The two are not merged and have no relationship to each other today. Do not
/// assume they represent the same "supplier" — see the remarks on <c>Supplier</c> for how they might
/// be connected later.
/// </remarks>
public sealed class InventorySupplier : AggregateRoot, IAuditable, ISoftDeletable
{
    private InventorySupplier()
    {
    }

    private InventorySupplier(
        Guid id,
        string companyName,
        string? contactPerson,
        string? email,
        string? phone,
        string? website,
        string? country,
        string currency,
        string? notes)
        : base(id)
    {
        CompanyName = companyName;
        ContactPerson = contactPerson;
        Email = email;
        Phone = phone;
        Website = website;
        Country = country;
        Currency = currency;
        Notes = notes;
        Status = SupplierStatus.Active;
    }

    public string CompanyName { get; private set; } = string.Empty;
    public string? ContactPerson { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Website { get; private set; }
    public string? Country { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? Notes { get; private set; }
    public SupplierStatus Status { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static InventorySupplier Create(
        string companyName,
        string? contactPerson = null,
        string? email = null,
        string? phone = null,
        string? website = null,
        string? country = null,
        string currency = "USD",
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        return new InventorySupplier(
            Guid.NewGuid(),
            companyName.Trim(),
            contactPerson?.Trim(),
            email?.Trim(),
            phone?.Trim(),
            website?.Trim(),
            country?.Trim(),
            currency.Trim().ToUpperInvariant(),
            notes?.Trim());
    }

    public void Update(
        string companyName,
        string? contactPerson,
        string? email,
        string? phone,
        string? website,
        string? country,
        string currency,
        string? notes,
        SupplierStatus status)
    {
        CompanyName = companyName.Trim();
        ContactPerson = contactPerson?.Trim();
        Email = email?.Trim();
        Phone = phone?.Trim();
        Website = website?.Trim();
        Country = country?.Trim();
        Currency = currency.Trim().ToUpperInvariant();
        Notes = notes?.Trim();
        Status = status;
    }
}
